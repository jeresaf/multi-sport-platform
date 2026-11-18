using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.Modules.Platform.Features.Media.ServeMedia;

/// <summary>
/// <c>GET /api/public/v1/media/{token}</c>: the local storage's signed URLs (S06 D17). Public (the token is the
/// permission, issued after the owning module's check) and not tenant-resolved from the host: the scope's tenant comes
/// from the token before anything is read, so the asset is read under RLS and a token can't reach another tenant's
/// file. Never cached. The token is a credential for its lifetime, so logs and traces record the route template. Every refusal (bad, expired, another tenant's, removed) is the same <c>MEDIA_NOT_FOUND</c>.
/// </summary>
internal static class ServeMediaEndpoint
{
    public const string Tag = "Media";

    public static void Map(IEndpointRouteBuilder publicApi) => publicApi
        .MapGet("/media/{token}", HandleAsync)
        .WithTags(Tag)
        .RedactPathInTelemetry()
        .WithName("GetMediaFile")
        .WithSummary("Get a media file by its signed URL")
        .WithDescription("Serves one stored file of a media asset while its signed URL is valid (10 minutes). Only signed URLs from an admin read lead here; the token names the tenant, the asset and the variant. Photo variants are served inline; documents (the original as uploaded, whatever its type) as a download. `Cache-Control: private, no-store`.")
        .Produces<Stream>(StatusCodes.Status200OK, "image/webp", "image/jpeg", "image/png", "application/pdf")
        .ProducesErrors(PlatformErrors.MediaNotFound);

    private static async Task<IResult> HandleAsync(
        string token,
        MediaUrlTokens tokens,
        TenantContext tenantContext,
        IMediaAssets media,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        if (tokens.Read(token) is not { } file)
        {
            return PlatformErrors.MediaNotFound.ToProblem();
        }

        // Before the first tenant-data connection of the request (S02 D1).
        tenantContext.Set(file.TenantId);
        var opened = await media.OpenAsync(file.AssetId, file.Variant, cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error.ToProblem();
        }

        var content = opened.Value;
        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        httpContext.Response.RegisterForDisposeAsync(content);
        // A document (the original as uploaded: consent evidence, a PDF or a photographed form) is a download; a photo's
        // variants are shown inline.
        return file.Variant == MediaVariant.Original
            ? Results.Stream(content.Content, content.ContentType, fileDownloadName: $"{file.AssetId:N}.{MediaRules.Extension(content.ContentType)}")
            : Results.Stream(content.Content, content.ContentType);
    }
}
