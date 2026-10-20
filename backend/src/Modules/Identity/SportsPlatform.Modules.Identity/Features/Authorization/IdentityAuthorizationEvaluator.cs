using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Step 3 of the decision formula, scoped permission (S03 D10), read per request (no claims cache, D5):
/// <list type="bullet">
/// <item>an account action is the caller's own account: allowed to a signed-in user, and to an anonymous caller when
/// the action allows one (the pipeline answers the others 401 first);</item>
/// <item>a platform action needs an unrevoked platform role that grants it;</item>
/// <item>a tenant action needs an active membership in the request's tenant and an unrevoked assignment whose role
/// grants the action, whose scope type can grant it (the action's <c>scopable_by</c>), and whose scope is the tenant
/// or, for a request on a resource, in that resource's scope chain. Read under the request's tenant (RLS). Platform
/// roles never grant tenant actions.</item>
/// </list>
/// The rule is <see cref="HeldRoles.Allows"/>, which the capabilities share.
/// </summary>
public sealed class IdentityAuthorizationEvaluator(Permissions permissions, ScopeChains scopeChains) : IAuthorizationEvaluator
{
    private static readonly IReadOnlySet<ScopeRef> _noResource = new HashSet<ScopeRef>();

    public async ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = request.Action.Scope switch
        {
            ActionScope.Account => request.Actor.Kind != ActorKind.Anonymous || request.Action.AllowsAnonymous,
            ActionScope.Platform => (await permissions.PlatformRolesAsync(request.Actor, cancellationToken)).Allows(request.Action, _noResource),
            ActionScope.Tenant => await AllowsTenantActionAsync(request, cancellationToken),
            _ => false,
        };
        return allowed ? Result.Success() : Result.Failure(AuthorizationErrors.Forbidden);
    }

    private async Task<bool> AllowsTenantActionAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        var held = await permissions.TenantRolesAsync(request.Actor, request.TenantId, cancellationToken);
        if (held.Allows(request.Action, _noResource))
        {
            return true;
        }

        // Only an assignment below the tenant is left to grant it: read the resource's chain only then.
        return request.Scope is { } resource
            && held.GrantsBelowTheTenant(request.Action)
            && held.Allows(request.Action, await scopeChains.ChainAsync(resource, cancellationToken));
    }
}
