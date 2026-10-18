using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// An integration event waiting for delivery, in the producing module's <c>outbox_messages</c>. Written in the same
/// transaction as the change (<see cref="ModuleDbContext.Publish"/>); the envelope is stamped when it is saved.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public const int LastErrorMaxLength = 2000;

    internal OutboxMessage(IntegrationEventName eventName, string payload, string aggregateType, Guid aggregateId)
        : base(SequentialIds.Next())
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

        EventName = eventName.Name;
        EventVersion = eventName.Version;
        Payload = payload;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        Status = OutboxMessageStatus.Pending;
    }

    private OutboxMessage()
    {
    }

    /// <summary>Publication order (identity column): the processor delivers by it, one aggregate's messages in turn.</summary>
    public long Sequence { get; private set; }

    public string EventName { get; private set; } = string.Empty;

    public int EventVersion { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? TenantId { get; private set; }

    public string? CorrelationId { get; private set; }

    public Guid? CausationId { get; private set; }

    public Guid ActorId { get; private set; }

    public string AggregateType { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    /// <summary>The event as JSON (<see cref="MessageSerialization"/>).</summary>
    public string Payload { get; private set; } = string.Empty;

    public OutboxMessageStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Why the last attempt failed: consumer and exception type and message, truncated.</summary>
    public string? LastError { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public IntegrationEventEnvelope ToEnvelope() => new(
        Id, new IntegrationEventName(EventName, EventVersion), OccurredAt, TenantId, CorrelationId, CausationId, ActorId,
        AggregateType, AggregateId);

    internal void Stamp(DateTimeOffset occurredAt, Guid? tenantId, string? correlationId, Guid? causationId, Guid actorId)
    {
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
        TenantId = tenantId;
        CorrelationId = correlationId;
        CausationId = causationId;
        ActorId = actorId;
    }

    internal void MarkProcessed(DateTimeOffset now)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = now;
        LastError = null;
    }

    /// <summary>
    /// <c>FAILED</c> → <c>PENDING</c> with its attempts reset, due now (re-drive). <see cref="LastError"/> stays until
    /// the next outcome, so the diagnostics still show why it failed.
    /// </summary>
    internal void Redrive(DateTimeOffset now)
    {
        if (Status != OutboxMessageStatus.Failed)
        {
            throw new InvalidOperationException($"Only a FAILED message can be re-driven; message {Id} is {Status}.");
        }

        Status = OutboxMessageStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
    }

    /// <summary>Counts a failed attempt: retried after <see cref="OutboxRetry.Delay"/>, or <c>FAILED</c> after the last.</summary>
    internal void RecordFailure(string error, DateTimeOffset now, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
        if (Attempts >= maxAttempts)
        {
            Status = OutboxMessageStatus.Failed;
        }
        else
        {
            NextAttemptAt = now + OutboxRetry.Delay(Attempts);
        }
    }
}
