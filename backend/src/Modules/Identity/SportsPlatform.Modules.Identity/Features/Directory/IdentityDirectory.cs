using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure.Directory;

namespace SportsPlatform.Modules.Identity.Features.Directory;

/// <summary>
/// The cross-tenant reads a request needs before its tenant is set (S03 D8), on <see cref="IdentityDirectoryDbContext"/>.
/// Each reads the memberships of one user, the caller, so it is not audited by itself; the request's own entry
/// covers it. Read per request (no cache, S03 D5): a removed or suspended membership is refused on the next request.
/// </summary>
public sealed class IdentityDirectory(IdentityDirectoryDbContext context, ICurrentActor actor) : ITenantMembershipChecker
{
    /// <summary>Tenant resolution's check for <c>X-Tenant-Id</c>: the signed-in user has an active membership there.</summary>
    public async ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken) =>
        actor.Kind == ActorKind.User
        && await context.Memberships.AnyAsync(
            m => m.TenantId == tenantId && m.UserId == actor.ActorId && m.Status == MembershipStatus.Active, cancellationToken);

    /// <summary>The tenants <paramref name="userId"/> is an active member of, with the codes of their unrevoked roles.</summary>
    public async Task<IReadOnlyList<MemberTenant>> TenantsOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await context.Memberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Select(m => new
            {
                m.TenantId,
                Roles = context.RoleAssignments
                    .Where(a => a.TenantId == m.TenantId && a.MembershipId == m.Id && a.RevokedAt == null)
                    .Select(a => a.RoleCode)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(row => new MemberTenant(row.TenantId, [.. row.Roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))
            .OrderBy(tenant => tenant.TenantId)];
    }
}

/// <summary>A tenant a user is an active member of, and the role codes they hold there.</summary>
public sealed record MemberTenant(Guid TenantId, IReadOnlyList<string> Roles);
