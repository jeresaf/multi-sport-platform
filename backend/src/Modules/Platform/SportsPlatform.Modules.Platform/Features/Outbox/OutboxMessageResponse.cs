using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox;

/// <summary>
/// One outbox message in the platform diagnostics: envelope and delivery state, no payload. <c>heldBackBy</c> is set on
/// a <c>PENDING</c> message that waits for an earlier, not yet processed message of its aggregate.
/// </summary>
public sealed record OutboxMessageResponse(
    string Module,
    Guid Id,
    long Sequence,
    string EventName,
    int EventVersion,
    string AggregateType,
    Guid AggregateId,
    Guid? TenantId,
    string? CorrelationId,
    DateTimeOffset OccurredAt,
    OutboxMessageStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    DateTimeOffset? ProcessedAt,
    Guid? HeldBackBy)
{
    public static OutboxMessageResponse From(OutboxMessageView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new(
            view.Module, view.Id, view.Sequence, view.EventName, view.EventVersion, view.AggregateType, view.AggregateId,
            view.TenantId, view.CorrelationId, view.OccurredAt, view.Status, view.Attempts, view.NextAttemptAt,
            view.LastError, view.ProcessedAt, view.HeldBackBy);
    }
}
