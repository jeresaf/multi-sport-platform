using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Members.ListMembers;

public sealed record ListMembersResponse(IReadOnlyList<MemberResponse> Items, PageInfo Page);

/// <summary>
/// The request's tenant's members (query filter and RLS), oldest first (membership IDs are UUIDv7). Without a status
/// filter, removed members are left out. Saves nothing, so the Host writes this read's audit entry after the response.
/// </summary>
public sealed class ListMembersHandler(IdentityDbContext context, AuditScope audit)
{
    public async Task<ListMembersResponse> HandleAsync(MemberQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        audit.Describe(metadata: new ListMembersMetadata(query.Status));

        var memberships = context.Set<TenantMembership>().AsNoTracking();
        memberships = query.Status is { } status
            ? memberships.Where(m => m.Status == status)
            : memberships.Where(m => m.Status != MembershipStatus.Removed);
        if (query.After is { } after)
        {
            memberships = memberships.Where(m => m.Id.CompareTo(after) > 0);
        }

        var page = await MemberResponse.Project(memberships.OrderBy(m => m.Id).Take(query.Limit + 1), context.Set<User>())
            .ToListAsync(cancellationToken);
        page = [.. page.OrderBy(m => m.MembershipId)];
        var hasMore = page.Count > query.Limit;
        var items = page.Take(query.Limit).ToList();
        return new ListMembersResponse(
            items, new PageInfo(hasMore ? ListMembersRequest.EncodeCursor(items[^1].MembershipId) : null, hasMore));
    }
}

/// <summary>The filter of the list, as this read's audit entry records it.</summary>
public sealed record ListMembersMetadata(MembershipStatus? Status);
