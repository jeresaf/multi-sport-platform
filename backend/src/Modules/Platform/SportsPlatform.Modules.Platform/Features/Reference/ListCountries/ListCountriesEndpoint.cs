using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListCountries;

internal static class ListCountriesEndpoint
{
    public static void Map(IEndpointRouteBuilder reference) => reference
        .MapGet("/countries", HandleAsync)
        .WithName("ListCountries")
        .WithSummary("List countries")
        .WithDescription("Active ISO 3166-1 countries, ordered by alpha-2 code. Cacheable for an hour; send the `ETag` back as `If-None-Match` to get 304.");

    private static async Task<CacheableJson<ListCountriesResponse>> HandleAsync(
        ListCountriesHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(cancellationToken), ReferenceEndpoints.MaxAge);
}
