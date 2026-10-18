using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

public static partial class MessagingServiceCollectionExtensions
{
    /// <summary>Handles a domain event inside the raising module, before its changes commit.</summary>
    public static IServiceCollection AddDomainEventSubscriber<TEvent, TSubscriber>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where TSubscriber : class, IDomainEventSubscriber<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDomainEventSubscriber<TEvent>, TSubscriber>();
        return services;
    }

    /// <summary>
    /// Subscribes a consumer to an integration event. <paramref name="consumer"/> is stored in
    /// <typeparamref name="TContext"/>'s inbox and must never change: <c>&lt;module&gt;.&lt;purpose&gt;</c>, e.g.
    /// <c>progression.standings</c>.
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, TConsumer, TContext>(this IServiceCollection services, string consumer)
        where TEvent : class, IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        if (consumer is null || !ConsumerName().IsMatch(consumer))
        {
            throw new ArgumentException($"Consumer '{consumer}' must look like '<module>.<purpose>' in snake_case.", nameof(consumer));
        }

        services.TryAddScoped<TConsumer>();
        services.AddSingleton(IntegrationEventSubscription.Create<TEvent, TConsumer, TContext>(consumer));
        return services;
    }

    /// <summary>The processor that delivers outbox messages (polled by the Platform module's hosted service).</summary>
    public static IServiceCollection AddOutboxProcessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<OutboxProcessor>();
        return services;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+\z")]
    private static partial Regex ConsumerName();
}
