using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure.Directory;

namespace SportsPlatform.Modules.Identity.Features.Directory;

/// <summary>
/// The cross-tenant reads a request needs before its tenant is set (S03 D8), on <see cref="IdentityDirectoryDbContext"/>.
/// The membership reads are of the caller's own memberships (the current actor; no method takes a user ID), so they are
/// not audited (owner, S03-T4a: an exemption from multi-tenancy.md's "audited" rule for sp_platform). Read per request
/// (no cache, S03 D5): a removed or suspended membership is refused on the next request. The invitation read by token
/// hash (T4c2) returns IDs only, for the invitation endpoints, which are audited.
/// </summary>
public sealed class IdentityDirectory(IdentityDirectoryDbContext context, ICurrentActor actor) : ITenantMembershipChecker
{
    /// <summary>Tenant resolution's check for <c>X-Tenant-Id</c>: the signed-in user has an active membership there.</summary>
    public async ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken) =>
        actor.Kind == ActorKind.User
        && await context.Memberships.AnyAsync(
            m => m.TenantId == tenantId && m.UserId == actor.ActorId && m.Status == MembershipStatus.Active, cancellationToken);

    /// <summary>The tenants the signed-in user is an active member of, with the codes of their unrevoked roles.</summary>
    public async Task<IReadOnlyList<MemberTenant>> MyTenantsAsync(CancellationToken cancellationToken)
    {
        if (actor.Kind != ActorKind.User)
        {
            return [];
        }

        var rows = await context.Memberships
            .Where(m => m.UserId == actor.ActorId && m.Status == MembershipStatus.Active)
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

    /// <summary>
    /// The invitation whose newest emailed token has this SHA-256, in any tenant (the hash is unique across tenants), or
    /// null. Its status isn't checked here: the caller reads the invitation under its tenant.
    /// </summary>
    public async Task<InvitationKey?> FindInvitationAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        return await context.Invitations
            .Where(i => i.TokenHash == tokenHash)
            .Select(i => new InvitationKey(i.Id, i.TenantId))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

/// <summary>An invitation found by its token, before its tenant is set: the caller then reads it under that tenant.</summary>
public sealed record InvitationKey(Guid InvitationId, Guid TenantId);

/// <summary>A tenant a user is an active member of, and the role codes they hold there.</summary>
public sealed record MemberTenant(Guid TenantId, IReadOnlyList<string> Roles);
