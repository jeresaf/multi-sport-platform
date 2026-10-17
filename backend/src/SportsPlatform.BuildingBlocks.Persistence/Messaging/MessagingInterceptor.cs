using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// Before a module's DbContext saves: dispatches the aggregates' domain events to their subscribers (round after round,
/// as subscribers may raise more), then stamps the envelope of new outbox messages. Everything lands in the same
/// transaction as the change. Registered before <see cref="AuditColumnsInterceptor"/>, so rows the subscribers add get
/// their audit columns too.
/// </summary>
public sealed class MessagingInterceptor(
    IServiceProvider services,
    ITenantContext tenant,
    CorrelationContext correlation,
    ICurrentActor actor,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
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
            StampNewOutboxMessages(context);
        }

        return result;
    }

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
