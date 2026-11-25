using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Platform.Features.Media.ServeMedia;

/// <summary>
/// <c>GET /api/public/v1/assets/{assetId}/{variant}</c> (S07 D15): a variant of a public asset (a logo or badge) of the
/// tenant whose site the host serves. It streams only an active <c>PUBLIC</c> asset of that tenant (read under RLS);
/// anything else (a private photo, a removed or unknown asset, another tenant's, a variant the asset hasn't) is the same
/// <c>404 MEDIA_NOT_FOUND</c>. The route doesn't know the owner: the owning module's public reads decide what they link.
/// Cached for a day, without <c>immutable</c>: a removed logo, or a site turned off, leaves caches within that time.
/// </summary>
internal static class ServePublicAssetEndpoint
{
    public const string Tag = "Public assets";

    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    /// <summary>The variants the route serves, by their path segment.</summary>
    private static readonly Dictionary<string, MediaVariant> _variants = new Dictionary<string, MediaVariant>(StringComparer.Ordinal)
    {
        ["thumbnail"] = MediaVariant.Thumbnail,
        ["medium"] = MediaVariant.Medium,
    };

    /// <summary>
    /// The route of a variant, relative to the API's origin (<see cref="IMediaAssets.GetPublicPath"/>). Only the
    /// thumbnail and the medium are served publicly: any other variant is a caller's bug.
    /// </summary>
    public static string PathOf(Guid assetId, MediaVariant variant) => variant switch
    {
        MediaVariant.Thumbnail or MediaVariant.Medium =>
            $"/api/public/v1/assets/{assetId}/{_variants.Single(pair => pair.Value == variant).Key}",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Only thumbnails and mediums are public."),
    };

    public static void Map(IEndpointRouteBuilder publicApi) => publicApi
        .MapGet("/assets/{assetId:guid}/{variant}", HandleAsync)
        .WithTags(Tag)
        .WithName("GetPublicAsset")
        .WithSummary("Get a public image (a logo or badge)")
        .WithDescription(
            "A variant (`thumbnail` 160 px or `medium` 640 px on the longest side) of a public image (an organisation's " +
            "logo, a team's logo or badge) of the tenant whose site this host serves, as WebP. Public reads link it; " +
            "anything else (a private file, a removed or unknown image, another tenant's, another variant) is " +
            "MEDIA_NOT_FOUND. `Cache-Control: public, max-age=86400`.")
        .RequirePublicTenant()
        .DocumentsPathValues("variant", _variants.Keys)
        .Produces<Stream>(StatusCodes.Status200OK, "image/webp")
        .ProducesErrors(PlatformErrors.MediaNotFound);

    private static async Task<IResult> HandleAsync(
        Guid assetId,
        string variant,
        IMediaAssets media,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!_variants.TryGetValue(variant, out var mediaVariant))
        {
            return PlatformErrors.MediaNotFound.ToProblem();
        }

        var opened = await media.OpenPublicAsync(assetId, mediaVariant, cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error.ToProblem();
        }

        var content = opened.Value;
        httpContext.Response.RegisterForDisposeAsync(content);
        httpContext.Response.Headers.CacheControl =
            string.Create(CultureInfo.InvariantCulture, $"public, max-age={(int)MaxAge.TotalSeconds}");
        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return Results.Stream(content.Content, content.ContentType);
    }
}
