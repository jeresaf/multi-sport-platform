using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>An outbox message as the platform diagnostics show it: the envelope and delivery state, never the payload.</summary>
public sealed record OutboxMessageView(
    string Module,
    Guid Id,
    long Sequence,
    string EventName,
    int EventVersion,
    string AggregateType,
    Guid AggregateId,
    Guid? TenantId,
    string? CorrelationId,
    DateTimeOffset OccurredAt,
    OutboxMessageStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    DateTimeOffset? ProcessedAt,
    Guid? HeldBackBy);

/// <summary>
/// Which messages to list, newest first. <paramref name="Module"/> is a module schema (all modules when null);
/// <paramref name="Before"/> is the last ID of the previous page.
/// </summary>
public sealed record OutboxQuery(string? Module, OutboxMessageStatus? Status, Guid? Before, int Limit);

/// <summary>One page of the diagnostics list.</summary>
/// <param name="Items">At most the query's limit, newest first.</param>
/// <param name="HasMore">Whether older messages match; the next page starts after the last item.</param>
public sealed record OutboxPage(IReadOnlyList<OutboxMessageView> Items, bool HasMore);

public enum OutboxRedriveOutcome
{
    Redriven,
    NotFound,
    NotFailed,
}

/// <summary>What a re-drive did.</summary>
/// <param name="Outcome">Whether the message was re-driven.</param>
/// <param name="Message">The message after the re-drive, or as it is when it was not <c>FAILED</c>.</param>
public sealed record OutboxRedriveResult(OutboxRedriveOutcome Outcome, OutboxMessageView? Message);

/// <summary>
/// Platform diagnostics over every module's <c>outbox_messages</c> (docs/architecture/events-and-jobs.md). Like the
/// <see cref="OutboxProcessor"/>, it reads each module's table through that module's own DbContext; it never joins
/// schemas. Message IDs are time-ordered (UUIDv7), so the modules' newest-first lists merge into one by ID.
/// </summary>
public sealed partial class OutboxDiagnostics(
    IServiceScopeFactory scopes, IEnumerable<ModuleDatabase> databases, TimeProvider timeProvider, ILogger<OutboxDiagnostics> logger)
{
    private readonly IReadOnlyList<ModuleDatabase> _databases = [.. databases];

    /// <summary>The module schemas whose outboxes can be listed and re-driven.</summary>
    public IReadOnlyList<string> Modules => [.. _databases.Select(database => database.Schema)];

    public async Task<OutboxPage> ListAsync(OutboxQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1);

        var items = new List<OutboxMessageView>();
        foreach (var database in _databases.Where(database => query.Module is null || database.Schema == query.Module))
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = Context(scope, database);
            var messages = context.Set<OutboxMessage>().AsNoTracking();
            if (query.Status is { } status)
            {
                messages = messages.Where(m => m.Status == status);
            }

            if (query.Before is { } before)
            {
                messages = messages.Where(m => m.Id.CompareTo(before) < 0);
            }

            // One more than the limit from each module tells whether the merged list has more.
            items.AddRange(await View(context, messages.OrderByDescending(m => m.Id).Take(query.Limit + 1))
                .ToListAsync(cancellationToken));
        }

        var page = items.OrderByDescending(item => item.Id).ToList();
        return new OutboxPage([.. page.Take(query.Limit)], page.Count > query.Limit);
    }

    /// <summary>
    /// Moves one <c>FAILED</c> message back to <c>PENDING</c> (docs/domain/state-machines.md, "Outbox message"). The row
    /// is locked first, so a processor that is failing it right now finishes before the status is checked.
    /// </summary>
    public async Task<OutboxRedriveResult> RedriveAsync(string module, Guid messageId, CancellationToken cancellationToken)
    {
        var database = _databases.SingleOrDefault(database => database.Schema == module);
        if (database is null)
        {
            return new OutboxRedriveResult(OutboxRedriveOutcome.NotFound, null);
        }

        await using var scope = scopes.CreateAsyncScope();
        var context = Context(scope, database);
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            var table = TableAccessMigrationExtensions.QualifiedName(context.Schema, MessagingModel.OutboxTable);
            // The table name is a validated identifier; the ID is a parameter.
            var sql = $"SELECT * FROM {table} WHERE id = {{0}} FOR UPDATE";
            var message = await context.Set<OutboxMessage>()
                .FromSqlRaw(sql, messageId)
                .SingleOrDefaultAsync(cancellationToken);
            if (message is null)
            {
                return new OutboxRedriveResult(OutboxRedriveOutcome.NotFound, null);
            }

            if (message.Status != OutboxMessageStatus.Failed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OutboxRedriveResult(OutboxRedriveOutcome.NotFailed, await FindAsync(context, messageId, cancellationToken));
            }

            message.Redrive(timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        LogRedriven(database.Schema, messageId);
        return new OutboxRedriveResult(OutboxRedriveOutcome.Redriven, await FindAsync(context, messageId, cancellationToken));
    }

    private static ModuleDbContext Context(AsyncServiceScope scope, ModuleDatabase database) =>
        (ModuleDbContext)scope.ServiceProvider.GetRequiredService(database.ContextType);

    private static async Task<OutboxMessageView> FindAsync(ModuleDbContext context, Guid messageId, CancellationToken cancellationToken) =>
        await View(context, context.Set<OutboxMessage>().AsNoTracking().Where(m => m.Id == messageId)).SingleAsync(cancellationToken);

    /// <summary>
    /// A <c>PENDING</c> message is held back by the earliest earlier message of its aggregate that is not
    /// <c>PROCESSED</c> (the processor's ordering rule).
    /// </summary>
    private static IQueryable<OutboxMessageView> View(ModuleDbContext context, IQueryable<OutboxMessage> messages)
    {
        var all = context.Set<OutboxMessage>();
        var module = context.Schema;
        return messages.Select(m => new OutboxMessageView(
            module,
            m.Id,
            m.Sequence,
            m.EventName,
            m.EventVersion,
            m.AggregateType,
            m.AggregateId,
            m.TenantId,
            m.CorrelationId,
            m.OccurredAt,
            m.Status,
            m.Attempts,
            m.NextAttemptAt,
            m.LastError,
            m.ProcessedAt,
            m.Status == OutboxMessageStatus.Pending
                ? all.Where(earlier => earlier.AggregateType == m.AggregateType && earlier.AggregateId == m.AggregateId
                        && earlier.Sequence < m.Sequence && earlier.Status != OutboxMessageStatus.Processed)
                    .OrderBy(earlier => earlier.Sequence)
                    .Select(earlier => (Guid?)earlier.Id)
                    .FirstOrDefault()
                : null));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox message {MessageId} in schema {Schema} re-driven")]
    private partial void LogRedriven(string schema, Guid messageId);
}
