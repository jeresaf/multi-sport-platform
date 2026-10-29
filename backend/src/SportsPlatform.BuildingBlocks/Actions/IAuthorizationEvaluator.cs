using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>
/// What is being authorized (S03 D10): the action, the actor, the request's resolved tenant (null when none is set) and,
/// for a tenant action on a resource, that resource, whose scope chain can grant the action.
/// </summary>
public sealed record AuthorizationRequest(ActionDefinition Action, ICurrentActor Actor, Guid? TenantId = null, ScopeRef? Scope = null);

/// <summary>A resource a role can be assigned at, such as a competition or a team: the start of its scope chain.</summary>
public sealed record ScopeRef(AssignmentScope Type, Guid Id);

/// <summary>
/// Step 3 of the decision formula (docs/architecture/authorization.md): whether the actor's scoped permissions allow the
/// action. The Host runs steps 1 (access mode) and 2 (entitlement) before it; Identity implements it.
/// </summary>
public interface IAuthorizationEvaluator
{
    ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
