using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Features.Directory;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

/// <summary>
/// Reads the signed-in user, their active platform roles and the tenants they are an active member of, with their roles
/// there (across tenants, through the identity directory) and each tenant's slug, name and status (Tenancy), ordered by
/// name.
/// </summary>
public sealed class GetMeHandler(
    IdentityDbContext context, IdentityDirectory directory, ITenantDirectory tenantDirectory, ICurrentActor actor)
{
    public async Task<Result<MeResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var user = await context.Set<User>().AsNoTracking()
            .Where(u => u.Id == actor.ActorId)
            .Select(u => new { u.Id, u.Email })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            // A session for a user that no longer exists.
            return AuthorizationErrors.Unauthenticated;
        }

        var platformRoles = await context.Set<PlatformRoleAssignment>().AsNoTracking()
            .Where(a => a.UserId == user.Id && a.RevokedAt == null)
            .Select(a => a.RoleCode)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        var memberships = await directory.MyTenantsAsync(cancellationToken);
        var tenants = await tenantDirectory.GetManyAsync([.. memberships.Select(m => m.TenantId)], cancellationToken);

        // Every tenant status is listed: the switcher shows a blocked tenant as such.
        return Result.Success(new MeResponse(
            user.Id,
            user.Email!,
            platformRoles,
            [.. memberships
                .Join(tenants, m => m.TenantId, t => t.Id, (m, t) => new MeTenantResponse(t.Id, t.Slug, t.DisplayName, t.Status, m.Roles))
                .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.TenantId)]));
    }
}
