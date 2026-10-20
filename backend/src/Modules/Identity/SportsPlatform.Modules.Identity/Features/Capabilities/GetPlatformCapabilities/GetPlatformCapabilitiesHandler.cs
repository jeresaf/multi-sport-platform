using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Identity.Features.Authorization;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetPlatformCapabilities;

/// <summary>Every platform action the caller's platform roles grant, decided as the evaluator decides (<see cref="HeldRoles.Allows"/>).</summary>
public sealed class GetPlatformCapabilitiesHandler(Permissions permissions, IActionCatalogue actions, ICurrentActor actor)
{
    private static readonly IReadOnlySet<ScopeRef> _noResource = new HashSet<ScopeRef>();

    public async Task<CapabilitiesResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var held = await permissions.PlatformRolesAsync(actor, cancellationToken);
        return new CapabilitiesResponse(
            [.. actions.All.Where(action => action.Scope == ActionScope.Platform && held.Allows(action, _noResource)).Select(action => action.Code.Value)],
            Scope: null);
    }
}
