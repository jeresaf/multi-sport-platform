using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Step 3 of the decision formula, scoped permission (S03 D10). An account action is the caller's own account: allowed
/// to a signed-in user, and to an anonymous caller when the action allows one (the pipeline answers the others 401
/// first). Platform actions (platform roles, T3) and tenant actions (memberships and role assignments, T4) are refused
/// until then.
/// </summary>
public sealed class IdentityAuthorizationEvaluator : IAuthorizationEvaluator
{
    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = request.Action.Scope == ActionScope.Account
            && (request.Actor.Kind != ActorKind.Anonymous || request.Action.AllowsAnonymous);
        return ValueTask.FromResult(allowed ? Result.Success() : Result.Failure(AuthorizationErrors.Forbidden));
    }
}
