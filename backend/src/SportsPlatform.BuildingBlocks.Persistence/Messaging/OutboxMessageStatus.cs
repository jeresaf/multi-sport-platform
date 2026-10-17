namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>Delivery status of an outbox message (docs/domain/state-machines.md, "Outbox message").</summary>
public enum OutboxMessageStatus
{
    Pending,
    Processed,
    Failed,
}
