using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Step 3 of the decision formula, scoped permission (S03 D10). An account action is the caller's own account: the
/// pipeline has already answered 401 to an anonymous caller unless the action allows one, so it is allowed. Platform
/// actions (platform roles, T3) and tenant actions (memberships and role assignments, T4) are refused until then.
/// </summary>
public sealed class IdentityAuthorizationEvaluator : IAuthorizationEvaluator
{
    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Action.Scope == ActionScope.Account
            ? Result.Success()
            : Result.Failure(AuthorizationErrors.Forbidden));
    }
}
