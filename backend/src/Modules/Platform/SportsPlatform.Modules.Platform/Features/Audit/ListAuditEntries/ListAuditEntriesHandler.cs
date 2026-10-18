using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

/// <summary>
/// The audit list's filters, all optional and combined with AND. <paramref name="From"/> is inclusive,
/// <paramref name="To"/> exclusive; <paramref name="CursorAt"/> and <paramref name="CursorId"/> are the last entry of the
/// previous page.
/// </summary>
public sealed record AuditQuery(
    Guid? TenantId,
    Guid? ActorId,
    string? Action,
    string? EntityType,
    Guid? EntityId,
    string? CorrelationId,
    AuditOutcome? Outcome,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Limit,
    DateTimeOffset? CursorAt,
    Guid? CursorId);

public sealed record ListAuditEntriesResponse(IReadOnlyList<AuditEntryResponse> Items, PageInfo Page);

/// <summary>
/// Lists audit entries of every tenant (and of none) on <c>sp_platform</c>, newest first by <c>(occurred_at, id)</c>.
/// Saves nothing, so the Host writes this read's own audit entry (with the filters) after the response; filtered to
/// one tenant, the entry has that tenant.
/// </summary>
public sealed class ListAuditEntriesHandler(PlatformAuditDbContext context, AuditScope audit)
{
    public async Task<ListAuditEntriesResponse> HandleAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        audit.Describe(metadata: ListAuditEntriesMetadata.Of(query), targetTenantId: query.TenantId);

        var entries = Filter(context.Set<AuditEntry>().AsNoTracking(), query);
        if (query is { CursorAt: { } at, CursorId: { } id })
        {
            // The redundant `<= at` gives the planner an index range on occurred_at; the OR alone is not one.
            entries = entries.Where(e => e.OccurredAt <= at
                && (e.OccurredAt < at || (e.OccurredAt == at && e.Id.CompareTo(id) < 0)));
        }

        var page = await entries
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken);
        var hasMore = page.Count > query.Limit;
        var items = page.Take(query.Limit).Select(AuditEntryResponse.From).ToList();
        var nextCursor = hasMore ? AuditCursor.Encode(items[^1].OccurredAt, items[^1].Id) : null;
        return new ListAuditEntriesResponse(items, new PageInfo(nextCursor, hasMore));
    }

    private static IQueryable<AuditEntry> Filter(IQueryable<AuditEntry> entries, AuditQuery query)
    {
        if (query.TenantId is { } tenantId)
        {
            entries = entries.Where(e => e.TenantId == tenantId);
        }

        if (query.ActorId is { } actorId)
        {
            entries = entries.Where(e => e.ActorId == actorId);
        }

        if (query.Action is { } action)
        {
            entries = entries.Where(e => e.ActionCode == action);
        }

        if (query.EntityType is { } entityType)
        {
            entries = entries.Where(e => e.EntityType == entityType);
        }

        if (query.EntityId is { } entityId)
        {
            entries = entries.Where(e => e.EntityId == entityId);
        }

        if (query.CorrelationId is { } correlationId)
        {
            entries = entries.Where(e => e.CorrelationId == correlationId);
        }

        if (query.Outcome is { } outcome)
        {
            entries = entries.Where(e => e.Outcome == outcome);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(e => e.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(e => e.OccurredAt < to);
        }

        return entries;
    }
}

/// <summary>The filters of the list, as this read's own audit entry records them.</summary>
public sealed record ListAuditEntriesMetadata(
    Guid? TenantId,
    Guid? ActorId,
    string? Action,
    string? EntityType,
    Guid? EntityId,
    string? CorrelationId,
    AuditOutcome? Outcome,
    DateTimeOffset? From,
    DateTimeOffset? To)
{
    public static ListAuditEntriesMetadata Of(AuditQuery query) => new(
        query.TenantId, query.ActorId, query.Action, query.EntityType, query.EntityId, query.CorrelationId,
        query.Outcome, query.From, query.To);
}
