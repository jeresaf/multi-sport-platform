using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Site.GetSite;

internal static class GetSiteEndpoint
{
    /// <summary>No longer than the Host's host cache keeps a tenant, so a hidden site leaves HTTP caches as fast.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    public static void Map(IEndpointRouteBuilder publicApi) => publicApi
        .MapGet("/site", HandleAsync)
        .WithName("GetSite")
        .WithTags("Site")
        .WithSummary("Get the public site")
        .WithDescription(
            "The tenant whose site this host serves: slug, display name and primary domain. The tenant comes from the " +
            "request host (`X-Forwarded-Host` from a trusted proxy). Responses differ per host. Cacheable for a minute; " +
            "send the `ETag` back as `If-None-Match` to get 304.")
        .RequirePublicTenant()
        .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.Unavailable);

    private static async Task<CacheableJson<SiteResponse>> HandleAsync(GetSiteHandler handler, CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(cancellationToken), MaxAge);
}
