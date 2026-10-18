namespace SportsPlatform.BuildingBlocks.Messaging;

/// <summary>
/// A consumer of an integration event. It runs in its own scope, under the event's tenant, inside a transaction on its
/// module's DbContext that also records the event in that module's inbox, so a duplicate delivery is skipped. Make
/// changes through that DbContext; the processor saves them.
/// </summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
