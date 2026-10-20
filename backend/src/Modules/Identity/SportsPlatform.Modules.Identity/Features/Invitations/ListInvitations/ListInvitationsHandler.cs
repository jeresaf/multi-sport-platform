using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Invitations.ListInvitations;

public sealed record ListInvitationsResponse(IReadOnlyList<InvitationResponse> Items, PageInfo Page);

/// <summary>The filter of the list, as this read's audit entry records it.</summary>
public sealed record ListInvitationsMetadata(InvitationState? Status);

/// <summary>
/// The request's tenant's invitations (query filter and RLS), oldest first (IDs are UUIDv7). Without a status, those
/// still pending, expired ones included. Saves nothing, so the Host writes this read's audit entry after the response.
/// </summary>
public sealed class ListInvitationsHandler(IdentityDbContext context, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<ListInvitationsResponse> HandleAsync(InvitationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        audit.Describe(metadata: new ListInvitationsMetadata(query.State));

        var now = timeProvider.GetUtcNow();
        var invitations = context.Set<Invitation>().AsNoTracking();
        invitations = query.State switch
        {
            null => invitations.Where(i => i.Status == InvitationStatus.Pending),
            InvitationState.Pending => invitations.Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt > now),
            InvitationState.Expired => invitations.Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= now),
            InvitationState.Accepted => invitations.Where(i => i.Status == InvitationStatus.Accepted),
            InvitationState.Revoked => invitations.Where(i => i.Status == InvitationStatus.Revoked),
            _ => throw new InvalidOperationException($"Unknown invitation state {query.State}."),
        };
        if (query.After is { } after)
        {
            invitations = invitations.Where(i => i.Id.CompareTo(after) > 0);
        }

        var rows = await invitations.OrderBy(i => i.Id).Take(query.Limit + 1)
            .Select(i => new { Invitation = i, Version = EF.Property<uint>(i, AuditColumns.Version) })
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > query.Limit;
        var items = rows.Take(query.Limit).Select(row => InvitationResponse.From(row.Invitation, row.Version, now)).ToList();
        return new ListInvitationsResponse(items, new PageInfo(hasMore ? IdCursor.Encode(items[^1].InvitationId) : null, hasMore));
    }
}
