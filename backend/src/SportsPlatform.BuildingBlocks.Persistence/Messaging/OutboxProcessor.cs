using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// Delivers every module's due outbox messages to their subscribers (at least once; consumers are deduplicated by
/// their inbox). Each module's batch is claimed with <c>FOR UPDATE SKIP LOCKED</c> in one transaction, so two
/// processors never deliver the same message at the same time. Per aggregate, messages go in publication order: one
/// waits while an earlier message of its aggregate is not yet <c>PROCESSED</c> (retrying, locked by another processor,
/// or <c>FAILED</c> until re-driven), so a batch holds at most one message per aggregate.
/// </summary>
public sealed partial class OutboxProcessor
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IReadOnlyList<ModuleDatabase> _databases;
    private readonly ILookup<(string Name, int Version), IntegrationEventSubscription> _subscriptions;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopes,
        IEnumerable<ModuleDatabase> databases,
        IEnumerable<IntegrationEventSubscription> subscriptions,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        var all = subscriptions.ToList();
        var duplicates = all.GroupBy(s => (s.Consumer, s.EventName)).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Consumers subscribed more than once to the same event: {string.Join(", ", duplicates.Select(d => $"{d.Consumer} → {d.EventName}"))}.");
        }

        _scopes = scopes;
        _databases = [.. databases];
        _subscriptions = all.ToLookup(s => (s.EventName.Name, s.EventName.Version));
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes one batch per module; returns how many messages were attempted. A module that fails (e.g. its schema is
    /// not migrated yet) is logged and skipped, so the others still deliver.
    /// </summary>
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        var attempted = 0;
        foreach (var database in _databases)
        {
            try
            {
                attempted += await ProcessModuleAsync(database, cancellationToken);
            }
#pragma warning disable CA1031 // One module's outage must not stop delivery for the others.
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
            {
                LogModuleFailed(exception, database.Schema);
            }
        }

        return attempted;
    }

    private async Task<int> ProcessModuleAsync(ModuleDatabase database, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(database.ContextType);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var table = TableAccessMigrationExtensions.QualifiedName(context.Schema, MessagingModel.OutboxTable);
        // The table name is a validated identifier; the values are parameters. The NOT EXISTS reads earlier messages
        // whether or not another processor has locked them, so a locked earlier message holds its successors back too.
        var sql =
            $$"""
            SELECT m.* FROM {{table}} AS m
            WHERE m.status = 'PENDING' AND m.next_attempt_at <= {0}
              AND NOT EXISTS (
                SELECT 1 FROM {{table}} AS earlier
                WHERE earlier.aggregate_type = m.aggregate_type AND earlier.aggregate_id = m.aggregate_id
                  AND earlier.sequence < m.sequence AND earlier.status <> 'PROCESSED')
            ORDER BY m.sequence
            LIMIT {1}
            FOR UPDATE OF m SKIP LOCKED
            """;
        var batch = await context.Set<OutboxMessage>()
            .FromSqlRaw(sql, _timeProvider.GetUtcNow(), _options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            var errors = await DeliverAsync(message, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            if (errors.Count == 0)
            {
                message.MarkProcessed(now);
                continue;
            }

            message.RecordFailure(string.Join("; ", errors), now, _options.MaxAttempts);
            if (message.Status == OutboxMessageStatus.Failed)
            {
                LogFailed(context.Schema, message.Id, message.EventName, message.EventVersion, message.Attempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    /// <summary>Delivers to every subscriber; returns one error per subscriber that failed.</summary>
    private async Task<List<string>> DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var subscription in _subscriptions[(message.EventName, message.EventVersion)])
        {
            try
            {
                if (!await subscription.DeliverAsync(_scopes, message, cancellationToken))
                {
                    LogDuplicate(subscription.Consumer, message.Id);
                }
            }
#pragma warning disable CA1031 // Any consumer failure (bad payload, bug, database) is a failed attempt, retried later.
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
            {
                LogAttemptFailed(exception, subscription.Consumer, message.Id, message.EventName, message.EventVersion);
                errors.Add($"{subscription.Consumer}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        return errors;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer {Consumer} failed to handle message {MessageId} ({EventName} v{EventVersion})")]
    private partial void LogAttemptFailed(Exception exception, string consumer, Guid messageId, string eventName, int eventVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({EventName} v{EventVersion}) in schema {Schema} is FAILED after {Attempts} attempts")]
    private partial void LogFailed(string schema, Guid messageId, string eventName, int eventVersion, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox processing failed for schema {Schema}; the other modules continue")]
    private partial void LogModuleFailed(Exception exception, string schema);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumer {Consumer} already handled message {MessageId}")]
    private partial void LogDuplicate(string consumer, Guid messageId);
}
