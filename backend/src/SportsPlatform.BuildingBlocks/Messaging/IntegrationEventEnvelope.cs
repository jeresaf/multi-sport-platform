namespace SportsPlatform.BuildingBlocks.Messaging;

/// <summary>Who, when and where of an integration event (ADR-0009); the payload is the event itself.</summary>
public sealed record IntegrationEventEnvelope(
    Guid EventId,
    IntegrationEventName EventName,
    DateTimeOffset OccurredAt,
    Guid? TenantId,
    string? CorrelationId,
    Guid? CausationId,
    Guid ActorId,
    string AggregateType,
    Guid AggregateId);
