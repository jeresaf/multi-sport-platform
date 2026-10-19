using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// Before a module's DbContext saves: dispatches the aggregates' domain events to their subscribers (round after round,
/// as subscribers may raise more), queues the audit entries (the request's <see cref="AuditScope"/>, if its entry is not
/// recorded yet, and any <see cref="ModuleDbContext.RecordAudit"/>) as <c>AuditRecorded</c> outbox messages, then stamps
/// the envelope of new outbox messages. Everything lands in the same transaction as the change. Registered before
/// <see cref="AuditColumnsInterceptor"/>, so rows the subscribers add get their audit columns too.
/// </summary>
public sealed class MessagingInterceptor(
    IServiceProvider services,
    ITenantContext tenant,
    CorrelationContext correlation,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    // The audit messages of the save under way; detached again if it fails or is cancelled, so a later save can't
    // commit them. One field: the interceptor is shared by the scope's DbContexts, whose saves run one after another.
    private InFlightAudit? _inFlight;

    /// <summary>More rounds than this means subscribers keep raising events for each other.</summary>
    public const int MaxDispatchRounds = 10;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> _dispatchers = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            if (PendingAggregates(context).Count > 0)
            {
                throw new InvalidOperationException("Domain event subscribers are asynchronous; save with SaveChangesAsync.");
            }

            QueueAudits(context);
            StampNewOutboxMessages(context);
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            await DispatchDomainEventsAsync(context, cancellationToken);
            (context as ModuleDbContext)?.EnsureNewEntitiesHaveIds();
            QueueAudits(context);
            StampNewOutboxMessages(context);
        }

        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        CompleteAudits(eventData);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        CompleteAudits(eventData);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => DiscardAudits(eventData);

    public override void SaveChangesCanceled(DbContextEventData eventData) => DiscardAudits(eventData);

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        DiscardAudits(eventData);
        return Task.CompletedTask;
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DiscardAudits(eventData);
        return Task.CompletedTask;
    }

    private void QueueAudits(DbContext context)
    {
        if (context is not ModuleDbContext module)
        {
            return;
        }

        var records = module.TakePendingAudits();
        var fromScope = audit.IsPending && _inFlight is null;
        if (fromScope)
        {
            records.Add(audit.CreateRecord(audit.SaveOutcome));
        }

        if (records.Count == 0)
        {
            return;
        }

        var factory = services.GetService<IAuditEventFactory>()
            ?? throw new InvalidOperationException($"No {nameof(IAuditEventFactory)} is registered; the Platform module provides it.");
        var messages = records
            .Select(record => module.AddOutboxMessage(factory.Create(record), IAuditEventFactory.AggregateType, record.Id))
            .ToList();
        _inFlight = new InFlightAudit(context, messages, fromScope);
    }

    private void CompleteAudits(DbContextEventData eventData)
    {
        if (_inFlight is { } inFlight && inFlight.Context == eventData.Context)
        {
            _inFlight = null;
            if (inFlight.FromScope)
            {
                audit.MarkRecorded();
            }
        }
    }

    private void DiscardAudits(DbContextEventData eventData)
    {
        if (_inFlight is { } inFlight && inFlight.Context == eventData.Context)
        {
            _inFlight = null;
            foreach (var message in inFlight.Messages)
            {
                inFlight.Context.Entry(message).State = EntityState.Detached;
            }
        }
    }

    private sealed record InFlightAudit(DbContext Context, List<OutboxMessage> Messages, bool FromScope);

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        for (var round = 0; ; round++)
        {
            var aggregates = PendingAggregates(context);
            if (aggregates.Count == 0)
            {
                return;
            }

            if (round == MaxDispatchRounds)
            {
                throw new InvalidOperationException(
                    $"Domain events were still being raised after {MaxDispatchRounds} rounds; do two subscribers keep raising events for each other?");
            }

            var events = new List<IDomainEvent>();
            foreach (var aggregate in aggregates)
            {
                events.AddRange(aggregate.DomainEvents);
                aggregate.ClearDomainEvents();
            }

            foreach (var domainEvent in events)
            {
                var dispatch = _dispatchers.GetOrAdd(domainEvent.GetType(), CreateDispatcher);
                await dispatch(services, domainEvent, cancellationToken);
            }
        }
    }

    private static List<AggregateRoot> PendingAggregates(DbContext context) =>
        [.. context.ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity).Where(aggregate => aggregate.DomainEvents.Count > 0)];

    private void StampNewOutboxMessages(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<OutboxMessage>().Where(entry => entry.State == EntityState.Added))
        {
            entry.Entity.Stamp(now, tenant.TenantId, correlation.CorrelationId, correlation.CausationId, actor.ActorId);
        }
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateDispatcher(Type eventType) =>
        typeof(MessagingInterceptor)
            .GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(eventType)
            .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var subscriber in services.GetServices<IDomainEventSubscriber<TEvent>>())
        {
            await subscriber.HandleAsync((TEvent)domainEvent, cancellationToken);
        }
    }
}
