using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListCurrencies;

internal static class ListCurrenciesEndpoint
{
    public static void Map(IEndpointRouteBuilder reference) => reference
        .MapGet("/currencies", HandleAsync)
        .WithName("ListCurrencies")
        .WithSummary("List currencies")
        .WithDescription("Active ISO 4217 currencies (money only), ordered by code, with their minor units. Cacheable for an hour; send the `ETag` back as `If-None-Match` to get 304.");

    private static async Task<CacheableJson<ListCurrenciesResponse>> HandleAsync(
        ListCurrenciesHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(cancellationToken), ReferenceEndpoints.MaxAge);
}
