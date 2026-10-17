namespace SportsPlatform.BuildingBlocks.Messaging;

/// <summary>
/// The correlation ID of the current work (the request's <c>X-Correlation-Id</c>, or the event's when consuming) and
/// the ID of the event that caused it. Scoped; set once by the Host or the outbox processor.
/// </summary>
public sealed class CorrelationContext
{
    public string? CorrelationId { get; private set; }

    public Guid? CausationId { get; private set; }

    public void Set(string? correlationId, Guid? causationId)
    {
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}
