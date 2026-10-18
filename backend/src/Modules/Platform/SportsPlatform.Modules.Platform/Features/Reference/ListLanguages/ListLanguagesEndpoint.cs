using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListLanguages;

internal static class ListLanguagesEndpoint
{
    public static void Map(IEndpointRouteBuilder reference) => reference
        .MapGet("/languages", HandleAsync)
        .WithName("ListLanguages")
        .WithSummary("List languages")
        .WithDescription("Active ISO 639-1 languages, ordered by code. Cacheable for an hour; send the `ETag` back as `If-None-Match` to get 304.");

    private static async Task<CacheableJson<ListLanguagesResponse>> HandleAsync(
        ListLanguagesHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(cancellationToken), ReferenceEndpoints.MaxAge);
}
