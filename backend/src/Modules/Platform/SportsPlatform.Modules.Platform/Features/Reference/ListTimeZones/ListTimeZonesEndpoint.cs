using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListTimeZones;

internal static class ListTimeZonesEndpoint
{
    public static void Map(IEndpointRouteBuilder reference) => reference
        .MapGet("/time-zones", HandleAsync)
        .WithName("ListTimeZones")
        .WithSummary("List time zones")
        .WithDescription(
            "Active IANA time zones, ordered by ID. `country` (ISO 3166-1 alpha-2, upper case) keeps only that country's zones; " +
            "a country without zones gives an empty list. Cacheable for an hour; send the `ETag` back as `If-None-Match` to get 304.")
        .ValidateRequest<ListTimeZonesRequest>(CommonErrors.InvalidQuery);

    private static async Task<CacheableJson<ListTimeZonesResponse>> HandleAsync(
        [AsParameters] ListTimeZonesRequest request,
        ListTimeZonesHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(request.Country, cancellationToken), ReferenceEndpoints.MaxAge);
}
