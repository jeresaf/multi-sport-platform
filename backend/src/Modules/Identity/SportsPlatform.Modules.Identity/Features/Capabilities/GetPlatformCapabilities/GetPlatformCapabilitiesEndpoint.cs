using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetPlatformCapabilities;

internal static class GetPlatformCapabilitiesEndpoint
{
    public static void Map(IEndpointRouteBuilder capabilities) => capabilities
        .MapGet("", HandleAsync)
        .WithName("GetPlatformCapabilities")
        .WithSummary("List the platform actions the signed-in user may perform")
        .WithDescription(
            "The codes of the platform actions (docs/generated/actions.md) the signed-in user's platform roles grant, in " +
            "ordinal order. The platform console shows only what this list allows; every request is still authorized.")
        .RequireAction(IdentityActions.PlatformCapabilitiesRead);

    private static async Task<Ok<CapabilitiesResponse>> HandleAsync(
        GetPlatformCapabilitiesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
