using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>A module's scope chain for one resource of <paramref name="type"/>: <paramref name="ancestors"/> above it, nothing for any other.</summary>
internal sealed class FakeScopeChain(AssignmentScope type, Guid resourceId, params ScopeRef[] ancestors) : IScopeChainProvider
{
    public AssignmentScope ResourceType => type;

    public Task<IReadOnlyList<ScopeRef>> GetAncestorsAsync(Guid resourceId1, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScopeRef>>(resourceId1 == resourceId ? ancestors : []);
}
