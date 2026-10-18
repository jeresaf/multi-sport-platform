using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.BuildingBlocks.Auditing;

/// <summary>How an audited action ended.</summary>
public enum AuditOutcome
{
    Success,

    /// <summary>Allowed, but it failed (validation, a domain rule, an unexpected error); nothing changed.</summary>
    Failure,

    /// <summary>Refused by authorization (401 or 403).</summary>
    Denied,
}

/// <summary>Where the action came from.</summary>
public enum AuditSource
{
    /// <summary>An HTTP request to the API.</summary>
    Api,

    /// <summary>Work with no request: CLI, seeds, outbox consumers, scheduled jobs.</summary>
    System,
}

/// <summary>
/// One audit entry before it is stored in <c>audit.audit_entries</c> (docs/architecture/events-and-jobs.md, "Audit uses
/// the same path"). <paramref name="Id"/> is a UUIDv7 taken when the action happened, so entries sort by time.
/// <paramref name="Metadata"/> is JSON that <see cref="AuditMetadata"/> has already redacted.
/// </summary>
public sealed record AuditRecord(
    Guid Id,
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
    string? Metadata)
{
    public const int ReasonMaxLength = 500;

    public const int EntityTypeMaxLength = 100;
}
