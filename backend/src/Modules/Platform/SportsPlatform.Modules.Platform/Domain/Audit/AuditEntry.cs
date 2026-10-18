using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Audit;

/// <summary>
/// One row of <c>audit.audit_entries</c>: who did which action to what, when, why and how it ended (AR-19). Append-only:
/// the table rejects UPDATE, DELETE and TRUNCATE for every role. Not tenant-filtered yet; RLS arrives with S02.
/// </summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry(AuditRecord record)
        : base(record.Id)
    {
        ActionCode = record.ActionCode;
        Outcome = record.Outcome;
        ErrorCode = record.ErrorCode;
        ActorKind = record.ActorKind;
        ActorId = record.ActorId;
        TenantId = record.TenantId;
        EntityType = record.EntityType;
        EntityId = record.EntityId;
        OccurredAt = record.OccurredAt;
        Source = record.Source;
        CorrelationId = record.CorrelationId;
        Reason = record.Reason;
        Metadata = record.Metadata;
    }

    private AuditEntry()
    {
    }

    public string ActionCode { get; private set; } = string.Empty;

    public AuditOutcome Outcome { get; private set; }

    public string? ErrorCode { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    /// <summary>The tenant the action was performed in, or the target tenant of a platform action; null for none.</summary>
    public Guid? TenantId { get; private set; }

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditSource Source { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>JSON with personal data masked (<see cref="AuditMetadata"/>).</summary>
    public string? Metadata { get; private set; }

    public static AuditEntry From(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new AuditEntry(record);
    }
}
