using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Registry.Features.Media;

/// <summary>
/// A logo or badge as the admin reads it (S07 D15): signed URLs of its two variants (no <c>large</c>), valid until
/// <c>expiresAt</c>, as a person's photo. <c>INTERNAL</c>: the URLs are signed for the admin; public reads link the public
/// asset route instead (S07-T5).
/// </summary>
public sealed record RegistryImageResponse(
    [property: PersonalData(PersonalDataClass.Internal)] string ThumbnailUrl,
    [property: PersonalData(PersonalDataClass.Internal)] string MediumUrl,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset ExpiresAt);

/// <summary>
/// The audit metadata of an image change: the new and the replaced asset by ID, as a person's photo. A removal has no new
/// asset, and the removed one is <c>replacedAssetId</c>.
/// </summary>
public sealed record RegistryImageAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? AssetId,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ReplacedAssetId);

/// <summary>Signs the URLs of an organisation's or a team's image for the admin reads (S07 D15).</summary>
public sealed partial class RegistryImageUrls(IMediaAssets media, ILogger<RegistryImageUrls> logger)
{
    /// <summary>
    /// The image's URLs, null without one or when Platform doesn't serve both variants: logged, since a record names
    /// only an active asset (a read racing a replacement can see the old one removed). The read still answers.
    /// </summary>
    public async Task<RegistryImageResponse?> SignAsync(
        string entityType, Guid entityId, Guid? assetId, CancellationToken cancellationToken)
    {
        if (assetId is not { } id)
        {
            return null;
        }

        var thumbnail = await media.GetSignedUrlAsync(id, MediaVariant.Thumbnail, cancellationToken);
        var medium = await media.GetSignedUrlAsync(id, MediaVariant.Medium, cancellationToken);
        if (thumbnail.IsFailure || medium.IsFailure)
        {
            LogImageNotServed(entityType, entityId, id, (thumbnail.IsFailure ? thumbnail.Error : medium.Error)!.Code);
            return null;
        }

        return new(thumbnail.Value.Url, medium.Value.Url, new[] { thumbnail.Value.ExpiresAt, medium.Value.ExpiresAt }.Min());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{EntityType} {EntityId} names image asset {AssetId}, which Platform doesn't serve: {ErrorCode}")]
    private partial void LogImageNotServed(string entityType, Guid entityId, Guid assetId, string errorCode);
}
