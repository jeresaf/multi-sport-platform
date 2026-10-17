using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>Turns the domain event into an integration event, in the same unit of work.</summary>
public sealed class PublishProbeCreated(MessagingTestDbContext context) : IDomainEventSubscriber<ProbeRegistered>
{
    public Task HandleAsync(ProbeRegistered domainEvent, CancellationToken cancellationToken)
    {
        context.Publish(new ProbeCreated.V1(domainEvent.ProbeId, domainEvent.Name), nameof(MessagingProbe), domainEvent.ProbeId);
        return Task.CompletedTask;
    }
}

public sealed class EchoAgain : IDomainEventSubscriber<ProbeEchoed>
{
    public Task HandleAsync(ProbeEchoed domainEvent, CancellationToken cancellationToken)
    {
        domainEvent.Probe.CountEcho();
        if (domainEvent.Remaining > 0)
        {
            domainEvent.Probe.Echo(domainEvent.Remaining - 1);
        }

        return Task.CompletedTask;
    }
}

/// <summary>What a consumer saw when it ran.</summary>
public sealed record Delivery(string Consumer, IntegrationEventEnvelope Envelope, Guid? TenantId, string? CorrelationId, Guid? CausationId);

/// <summary>Shared by the test consumers: what they were given, and whether the flaky one should throw.</summary>
public sealed class ConsumerLog
{
    private int _flakyFailuresLeft;

    public ConcurrentQueue<Delivery> Deliveries { get; } = new();

    public int FlakyFailuresLeft
    {
        get => Volatile.Read(ref _flakyFailuresLeft);
        set => Volatile.Write(ref _flakyFailuresLeft, value);
    }

    public IReadOnlyList<Delivery> For(string consumer) => [.. Deliveries.Where(d => d.Consumer == consumer)];

    public void Reset()
    {
        Deliveries.Clear();
        FlakyFailuresLeft = 0;
    }

    internal bool ConsumeFlakyFailure() => Interlocked.Decrement(ref _flakyFailuresLeft) >= 0;
}

/// <summary>Records the delivery and writes a row through the consumer module's DbContext.</summary>
public sealed class RecordingConsumer(MessagingTestDbContext context, ConsumerLog log, ITenantContext tenant, CorrelationContext correlation)
    : IIntegrationEventConsumer<ProbeCreated.V1>
{
    public const string Name = "messaging_test.recorder";

    public Task HandleAsync(ProbeCreated.V1 integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        log.Deliveries.Enqueue(new Delivery(Name, envelope, tenant.TenantId, correlation.CorrelationId, correlation.CausationId));
        context.ReceivedProbes.Add(new ReceivedProbe(integrationEvent.ProbeId, envelope.EventId));
        return Task.CompletedTask;
    }
}

/// <summary>Throws while <see cref="ConsumerLog.FlakyFailuresLeft"/> is above zero.</summary>
public sealed class FlakyConsumer(ConsumerLog log, ITenantContext tenant, CorrelationContext correlation)
    : IIntegrationEventConsumer<ProbeCreated.V1>
{
    public const string Name = "messaging_test.flaky";

    public Task HandleAsync(ProbeCreated.V1 integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        log.Deliveries.Enqueue(new Delivery(Name, envelope, tenant.TenantId, correlation.CorrelationId, correlation.CausationId));
        return log.ConsumeFlakyFailure()
            ? throw new InvalidOperationException("The flaky consumer failed on purpose.")
            : Task.CompletedTask;
    }
}

public static class MessagingTestServices
{
    public static IServiceCollection AddMessagingTestHandlers(this IServiceCollection services, ConsumerLog log)
    {
        services.AddSingleton(log);
        services.AddDomainEventSubscriber<ProbeRegistered, PublishProbeCreated>();
        services.AddDomainEventSubscriber<ProbeEchoed, EchoAgain>();
        services.AddIntegrationEventConsumer<ProbeCreated.V1, RecordingConsumer, MessagingTestDbContext>(RecordingConsumer.Name);
        services.AddIntegrationEventConsumer<ProbeCreated.V1, FlakyConsumer, MessagingTestDbContext>(FlakyConsumer.Name);
        return services;
    }
}
