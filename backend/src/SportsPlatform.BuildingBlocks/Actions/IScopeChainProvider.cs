namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>
/// The scope chain of one resource type (docs/architecture/authorization.md, "Roles and scopes"), implemented by the
/// module that owns the resource (from S06): given a contest, its competition and participating teams, and so on. The
/// evaluator grants a tenant action on a resource when an assignment's scope is the resource itself or one of these.
/// </summary>
public interface IScopeChainProvider
{
    /// <summary>
    /// The resource type this provider answers for; one provider per type (two for one type fail the request today; the
    /// first provider, S06, adds a startup check).
    /// </summary>
    AssignmentScope ResourceType { get; }

    /// <summary>
    /// The scopes above the resource in the request's tenant (not the resource itself, not the tenant); empty for a
    /// resource that is unknown there.
    /// </summary>
    Task<IReadOnlyList<ScopeRef>> GetAncestorsAsync(Guid resourceId, CancellationToken cancellationToken);
}
