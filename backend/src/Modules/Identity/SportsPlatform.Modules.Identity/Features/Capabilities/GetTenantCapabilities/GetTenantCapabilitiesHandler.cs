using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Features.Authorization;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

/// <summary>
/// Every tenant action the caller's roles in the request's tenant grant, tenant-wide or, with a resource, on that
/// resource through its scope chain: decided as the evaluator decides (<see cref="HeldRoles.Allows"/>). Step 3 only:
/// the endpoint runs only when the access mode is <c>FULL</c>, and entitlements arrive with S04.
/// </summary>
public sealed class GetTenantCapabilitiesHandler(
    Permissions permissions, ScopeChains scopeChains, IActionCatalogue actions, ICurrentActor actor, ITenantContext tenantContext)
{
    public async Task<CapabilitiesResponse> HandleAsync(ScopeRef? resource, CancellationToken cancellationToken)
    {
        var held = await permissions.TenantRolesAsync(actor, tenantContext.TenantId, cancellationToken);
        var chain = resource is null ? new HashSet<ScopeRef>() : await scopeChains.ChainAsync(resource, cancellationToken);
        return new CapabilitiesResponse(
            [.. actions.All.Where(action => action.Scope == ActionScope.Tenant && held.Allows(action, chain)).Select(action => action.Code.Value)],
            resource is null ? null : new CapabilityScopeResponse(resource.Type, resource.Id));
    }
}
