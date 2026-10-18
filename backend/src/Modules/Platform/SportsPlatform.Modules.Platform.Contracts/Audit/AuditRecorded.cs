using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Modules.Platform.Contracts.Audit;

/// <summary>
/// An audited action committed in the publishing module; the Platform module appends it to <c>audit.audit_entries</c>.
/// Every module publishes it, through its own outbox, in the transaction of the change (the command pipeline does it;
/// modules don't publish it by hand). <see cref="V1.Metadata"/> is JSON with personal data already masked.
/// </summary>
public static class AuditRecorded
{
    public sealed record V1(
        Guid EntryId,
        string ActionCode,
        AuditOutcome Outcome,
        string? ErrorCode,
        ActorKind ActorKind,
        Guid ActorId,
        Guid? TenantId,
        string? EntityType,
        Guid? EntityId,
        DateTimeOffset OccurredAt,
        AuditSource Source,
        string? CorrelationId,
        string? Reason,
        string? Metadata) : IIntegrationEvent
    {
        public static V1 From(AuditRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            return new V1(
                record.Id, record.ActionCode, record.Outcome, record.ErrorCode, record.ActorKind, record.ActorId,
                record.TenantId, record.EntityType, record.EntityId, record.OccurredAt, record.Source,
                record.CorrelationId, record.Reason, record.Metadata);
        }

        public AuditRecord ToRecord() => new(
            EntryId, ActionCode, Outcome, ErrorCode, ActorKind, ActorId, TenantId, EntityType, EntityId, OccurredAt,
            Source, CorrelationId, Reason, Metadata);
    }
}
