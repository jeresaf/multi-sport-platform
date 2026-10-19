using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.GetTenantSettings;

internal static class GetTenantSettingsEndpoint
{
    public static void Map(IEndpointRouteBuilder settings) => settings
        .MapGet("", HandleAsync)
        .WithName("GetTenantSettings")
        .WithSummary("Get the tenant's settings")
        .WithDescription("The settings of the tenant named by `X-Tenant-Id`, with their `version` (also the `ETag`).")
        .RequireAction(TenancyActions.TenantSettingsRead);

    private static async Task<Ok<TenantSettingsResponse>> HandleAsync(
        GetTenantSettingsHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var settings = await handler.HandleAsync(cancellationToken);
        settings.SetETag(response);
        return TypedResults.Ok(settings);
    }
}
