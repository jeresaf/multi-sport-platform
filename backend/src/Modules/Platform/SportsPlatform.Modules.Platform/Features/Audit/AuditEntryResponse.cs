using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Platform.Domain.Audit;

namespace SportsPlatform.Modules.Platform.Features.Audit;

/// <summary>
/// One audit entry as platform staff see it: every column, with <c>metadata</c> as the JSON object it was stored as
/// (personal data already masked when it was recorded).
/// </summary>
public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActionCode,
    AuditOutcome Outcome,
    string? ErrorCode,
    ActorKind ActorKind,
    Guid ActorId,
    Guid? TenantId,
    string? EntityType,
    Guid? EntityId,
    AuditSource Source,
    string? CorrelationId,
    string? Reason,
    JsonElement? Metadata)
{
    public static AuditEntryResponse From(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        JsonElement? metadata = null;
        if (entry.Metadata is { } json)
        {
            using var document = JsonDocument.Parse(json);
            metadata = document.RootElement.Clone();
        }

        return new(
            entry.Id, entry.OccurredAt, entry.ActionCode, entry.Outcome, entry.ErrorCode, entry.ActorKind, entry.ActorId,
            entry.TenantId, entry.EntityType, entry.EntityId, entry.Source, entry.CorrelationId, entry.Reason, metadata);
    }
}
