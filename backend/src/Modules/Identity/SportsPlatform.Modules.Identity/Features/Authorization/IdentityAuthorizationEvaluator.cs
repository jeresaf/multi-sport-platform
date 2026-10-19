using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Step 3 of the decision formula, scoped permission (S03 D10), read per request (no claims cache, D5):
/// <list type="bullet">
/// <item>an account action is the caller's own account: allowed to a signed-in user, and to an anonymous caller when
/// the action allows one (the pipeline answers the others 401 first);</item>
/// <item>a platform action needs an unrevoked platform role that grants it;</item>
/// <item>tenant actions (memberships and role assignments, T4) are refused until then. Platform roles never grant
/// them.</item>
/// </list>
/// </summary>
public sealed class IdentityAuthorizationEvaluator(IdentityDbContext context, RoleCatalogue roles) : IAuthorizationEvaluator
{
    public async ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = request.Action.Scope switch
        {
            ActionScope.Account => request.Actor.Kind != ActorKind.Anonymous || request.Action.AllowsAnonymous,
            ActionScope.Platform => await HasPlatformRoleGrantingAsync(request, cancellationToken),
            _ => false,
        };
        return allowed ? Result.Success() : Result.Failure(AuthorizationErrors.Forbidden);
    }

    private async Task<bool> HasPlatformRoleGrantingAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        if (request.Actor.Kind != ActorKind.User)
        {
            return false;
        }

        var held = await context.Set<PlatformRoleAssignment>().AsNoTracking()
            .Where(a => a.UserId == request.Actor.ActorId && a.RevokedAt == null)
            .Select(a => a.RoleCode)
            .ToListAsync(cancellationToken);
        return held.Any(code => roles.Find(code) is { Scope: RoleScope.Platform } role && role.Grants(request.Action));
    }
}
