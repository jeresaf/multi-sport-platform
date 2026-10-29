using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

internal static class GetTenantCapabilitiesEndpoint
{
    public static void Map(IEndpointRouteBuilder capabilities) => capabilities
        .MapGet("", HandleAsync)
        .WithName("GetTenantCapabilities")
        .WithSummary("List the tenant actions the signed-in user may perform, and those the tenant can't use now")
        .WithDescription(
            "What the signed-in user's roles grant in the tenant named by `X-Tenant-Id` (tenant actions, " +
            "docs/generated/actions.md). Without `scopeType`/`scopeId`, the actions granted tenant-wide; with them " +
            "(COMPETITION, TEAM or CONTEST and its ID), also those granted on that resource or the scopes above it. " +
            "`actions`: what the user may perform now, in ordinal order. `unavailable`: what the roles grant but the tenant " +
            "can't use now, each with the refusal a request would get: TENANT_READ_ONLY (the subscription has ended, was " +
            "cancelled or hasn't started) or ENTITLEMENT_REQUIRED (with the entitlement and the plans that include it, for " +
            "\"Requires PRO plan\"). Actions the roles don't grant are in neither list. `access`: the tenant's access mode, " +
            "the current subscription period's status and last days (null without a period) and the tenant's time zone, " +
            "for the banner. The admin app shows only what `actions` allows and disables what `unavailable` lists; every " +
            "request is still authorized. A read-only tenant answers this request; a blocked one, or a provisioning one for a " +
            "member who can't set it up, answers 403 with the reason (TENANT_BLOCKED, TENANT_PROVISIONING).")
        .DocumentsQueryValues("scopeType", GetTenantCapabilitiesRequest.ScopeTypes)
        .RequireAction(IdentityActions.TenantCapabilitiesRead)
        .ValidateRequest<GetTenantCapabilitiesRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<TenantCapabilitiesResponse>> HandleAsync(
        [AsParameters] GetTenantCapabilitiesRequest request,
        GetTenantCapabilitiesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToScope(), cancellationToken));
}
