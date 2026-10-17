using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>What is being authorized. The resource's scope chain is added with roles (S03).</summary>
public sealed record AuthorizationRequest(ActionDefinition Action, ICurrentActor Actor);

/// <summary>
/// Decides whether the actor may perform the action: access mode, entitlement, scoped permission
/// (docs/architecture/authorization.md, "Decision formula"). The Host's default denies everything.
/// </summary>
public interface IAuthorizationEvaluator
{
    ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
