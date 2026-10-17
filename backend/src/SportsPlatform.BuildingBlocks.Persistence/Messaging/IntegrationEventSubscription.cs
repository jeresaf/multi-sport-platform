using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>One consumer of one integration event, registered with <c>AddIntegrationEventConsumer</c>.</summary>
public sealed class IntegrationEventSubscription
{
    private readonly Func<IServiceScopeFactory, OutboxMessage, CancellationToken, Task<bool>> _deliver;

    private IntegrationEventSubscription(
        string consumer, IntegrationEventName eventName, Type contextType,
        Func<IServiceScopeFactory, OutboxMessage, CancellationToken, Task<bool>> deliver)
    {
        Consumer = consumer;
        EventName = eventName;
        ContextType = contextType;
        _deliver = deliver;
    }

    /// <summary>Stable name recorded in the inbox, e.g. <c>progression.standings</c>. Never rename it.</summary>
    public string Consumer { get; }

    public IntegrationEventName EventName { get; }

    /// <summary>The consumer module's DbContext, whose inbox records the delivery.</summary>
    public Type ContextType { get; }

    internal static IntegrationEventSubscription Create<TEvent, TConsumer, TContext>(string consumer)
        where TEvent : class, IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
        where TContext : ModuleDbContext =>
        new(consumer, IntegrationEventName.Of(typeof(TEvent)), typeof(TContext),
            (scopes, message, cancellationToken) => DeliverAsync<TEvent, TConsumer, TContext>(consumer, scopes, message, cancellationToken));

    /// <summary>Delivers the message unless the consumer's inbox already has it; false for a duplicate.</summary>
    internal Task<bool> DeliverAsync(IServiceScopeFactory scopes, OutboxMessage message, CancellationToken cancellationToken) =>
        _deliver(scopes, message, cancellationToken);

    private static async Task<bool> DeliverAsync<TEvent, TConsumer, TContext>(
        string consumer, IServiceScopeFactory scopes, OutboxMessage message, CancellationToken cancellationToken)
        where TEvent : class, IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
        where TContext : ModuleDbContext
    {
        var envelope = message.ToEnvelope();

        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TenantContext>().Set(envelope.TenantId);
        services.GetRequiredService<CorrelationContext>().Set(envelope.CorrelationId, envelope.EventId);

        var context = services.GetRequiredService<TContext>();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var inbox = context.Set<InboxMessage>();
        if (await inbox.AnyAsync(m => m.MessageId == envelope.EventId && m.Consumer == consumer, cancellationToken))
        {
            return false;
        }

        var integrationEvent = JsonSerializer.Deserialize<TEvent>(message.Payload, MessageSerialization.Options)
            ?? throw new JsonException($"The payload of {envelope.EventName} {envelope.EventId} is null.");
        await services.GetRequiredService<TConsumer>().HandleAsync(integrationEvent, envelope, cancellationToken);
        inbox.Add(new InboxMessage(envelope.EventId, consumer));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_inbox_messages",
        })
        {
            // Another delivery of the same message committed first; this one's changes roll back with the transaction.
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
