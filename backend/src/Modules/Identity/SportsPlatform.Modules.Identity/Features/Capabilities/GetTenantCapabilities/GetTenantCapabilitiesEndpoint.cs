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
        .WithSummary("List the tenant actions the signed-in user may perform")
        .WithDescription(
            "The codes of the tenant actions (docs/generated/actions.md) the signed-in user's roles grant in the tenant named " +
            "by `X-Tenant-Id`, in ordinal order. Without `scopeType`/`scopeId`, the actions granted tenant-wide; with them " +
            "(COMPETITION, TEAM or CONTEST and its ID), also those granted on that resource or the scopes above it. The admin " +
            "app shows only what this list allows; every request is still authorized. A tenant whose access is limited answers " +
            "this request 403 with the reason (TENANT_BLOCKED, TENANT_PROVISIONING).")
        .RequireAction(IdentityActions.TenantCapabilitiesRead)
        .ValidateRequest<GetTenantCapabilitiesRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<CapabilitiesResponse>> HandleAsync(
        [AsParameters] GetTenantCapabilitiesRequest request,
        GetTenantCapabilitiesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToScope(), cancellationToken));
}
