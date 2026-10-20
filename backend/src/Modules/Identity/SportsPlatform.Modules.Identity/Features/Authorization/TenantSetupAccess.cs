using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Who may work in a <c>PROVISIONING</c> tenant (S03 D11): a user with an <c>ACTIVE</c> membership and an unrevoked
/// tenant-wide assignment of one of <see cref="IdentityRoles.SetupRoles"/>. Read per request under the request's tenant
/// (RLS), as the evaluator reads roles.
/// </summary>
public sealed class TenantSetupAccess(IdentityDbContext context, ICurrentActor actor, ITenantContext tenantContext) : ITenantSetupAccess
{
    public async ValueTask<bool> CanSetUpAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId != tenantId)
        {
            throw new InvalidOperationException("Set the scope's tenant to this tenant before checking who may set it up.");
        }

        if (actor.Kind != ActorKind.User)
        {
            return false;
        }

        string[] roles = [.. IdentityRoles.SetupRoles];
        return await context.Set<TenantMembership>().AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UserId == actor.ActorId && m.Status == MembershipStatus.Active)
            .SelectMany(m => m.Roles)
            .AnyAsync(a => a.RevokedAt == null && a.ScopeType == AssignmentScope.Tenant && roles.Contains(a.RoleCode), cancellationToken);
    }
}
