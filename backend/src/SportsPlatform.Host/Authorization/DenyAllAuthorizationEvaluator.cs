using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Host.Authorization;

/// <summary>Until users, roles and entitlements exist (S02–S04), nothing is authorized: admin and platform endpoints fail closed.</summary>
internal sealed class DenyAllAuthorizationEvaluator : IAuthorizationEvaluator
{
    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure(AuthorizationErrors.Forbidden));
}
