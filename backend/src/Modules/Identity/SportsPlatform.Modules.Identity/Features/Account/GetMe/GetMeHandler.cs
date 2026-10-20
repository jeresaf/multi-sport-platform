using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Features.Directory;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

/// <summary>
/// Reads the signed-in user, their active platform roles and the tenants they are an active member of, with their roles
/// there (across tenants, through the identity directory).
/// </summary>
public sealed class GetMeHandler(IdentityDbContext context, IdentityDirectory directory, ICurrentActor actor)
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

        var tenants = await directory.TenantsOfAsync(user.Id, cancellationToken);
        return Result.Success(new MeResponse(
            user.Id, user.Email!, platformRoles, [.. tenants.Select(t => new MeTenantResponse(t.TenantId, t.Roles))]));
    }
}
