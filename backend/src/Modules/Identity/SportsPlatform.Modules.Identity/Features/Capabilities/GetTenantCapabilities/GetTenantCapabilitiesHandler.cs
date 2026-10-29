using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

/// <summary>
/// Every tenant action the caller's roles in the request's tenant grant, tenant-wide or, with a resource, on that
/// resource through its scope chain (step 3, decided as the evaluator decides: <see cref="HeldRoles.Allows"/>), split
/// by steps 1 and 2 (S04 D14): what the access mode lets through (<see cref="TenantAccess.Admits"/>) and whose boolean
/// or level entitlement the tenant can use is in <c>actions</c>; the rest is <c>unavailable</c> with the first refusal,
/// in the middleware's order. A quota entitlement passes step 2 (its handler counts, D10). Step 1 has let this request
/// through, so the tenant is set and not blocked.
/// </summary>
public sealed class GetTenantCapabilitiesHandler(
    Permissions permissions,
    ScopeChains scopeChains,
    IActionCatalogue actions,
    IEntitlementCatalogue entitlementCatalogue,
    ITenantAccess tenantAccess,
    IEntitlements entitlements,
    ICurrentActor actor,
    ITenantContext tenantContext)
{
    public async Task<TenantCapabilitiesResponse> HandleAsync(ScopeRef? resource, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Capabilities are read under a tenant.");
        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Step 1 has passed, so the tenant exists.");
        var held = await permissions.TenantRolesAsync(actor, tenantId, cancellationToken);
        var chain = resource is null ? new HashSet<ScopeRef>() : await scopeChains.ChainAsync(resource, cancellationToken);

        var allowed = new List<string>();
        var unavailable = new List<UnavailableActionResponse>();
        foreach (var action in actions.All.Where(action => action.Scope == ActionScope.Tenant && held.Allows(action, chain)))
        {
            if (!access.Admits(action))
            {
                unavailable.Add(new UnavailableActionResponse(action.Code.Value, TenantAccessErrorCodes.ReadOnly, Entitlement: null, Plans: null));
            }
            else if (await MissingEntitlementAsync(action, cancellationToken) is { } code)
            {
                unavailable.Add(new UnavailableActionResponse(
                    action.Code.Value, EntitlementErrorCodes.Required, code, await entitlements.PlansAllowingAsync(code, cancellationToken)));
            }
            else
            {
                allowed.Add(action.Code.Value);
            }
        }

        return new TenantCapabilitiesResponse(
            allowed,
            unavailable,
            CapabilityAccessResponse.From(access),
            resource is null ? null : new CapabilityScopeResponse(resource.Type, resource.Id));
    }

    /// <summary>Step 2: the boolean or level entitlement the action names when the tenant can't use it, else null.</summary>
    private async Task<string?> MissingEntitlementAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (action.Entitlement is not { } code)
        {
            return null;
        }

        var definition = entitlementCatalogue.Find(code)
            ?? throw new InvalidOperationException($"Action {action.Code.Value} names the undeclared entitlement {code}.");
        return definition.Type == EntitlementType.Quota || await entitlements.CanUseAsync(code, cancellationToken) ? null : code;
    }
}
