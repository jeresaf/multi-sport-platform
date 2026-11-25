using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Registry.Features.Media;

/// <summary>
/// A logo or badge as the public reads link it (S07 D15; owner, plan 2026-11-25): the paths of its two variants on the
/// public asset route, relative to the API's origin on the tenant's host. They don't expire; the route answers only while
/// the asset is active and public.
/// </summary>
public sealed record PublicImageResponse(
    [property: PersonalData(PersonalDataClass.Public)] string ThumbnailUrl,
    [property: PersonalData(PersonalDataClass.Public)] string MediumUrl);

/// <summary>
/// Builds <see cref="PublicImageResponse"/> through <see cref="IMediaAssets.GetPublicPath"/> (Registry never writes
/// Platform's route itself, ADR-0014). Nothing is read, so a directory page links its images without a query.
/// </summary>
public sealed class PublicImages(IMediaAssets media)
{
    /// <summary>The image's paths, or null without one.</summary>
    public PublicImageResponse? Of(Guid? assetId) =>
        assetId is { } id
            ? new PublicImageResponse(media.GetPublicPath(id, MediaVariant.Thumbnail), media.GetPublicPath(id, MediaVariant.Medium))
            : null;
}
