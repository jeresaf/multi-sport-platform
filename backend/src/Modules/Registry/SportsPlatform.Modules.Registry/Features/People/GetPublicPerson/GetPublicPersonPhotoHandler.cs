using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>
/// A variant of a public person's photo (S06 D17): checked as the public read is, on every request, so a withdrawn
/// <c>PHOTO</c> scope or a profile turned off answers <c>404</c> at once. A person the public read doesn't show is
/// <c>PERSON_NOT_FOUND</c>; one it shows without a photo (none, or no <c>PHOTO</c> consent) <c>PERSON_PHOTO_NOT_FOUND</c>.
/// </summary>
public sealed class GetPublicPersonPhotoHandler(PublicPeople people, IMediaAssets media)
{
    public async Task<Result<MediaContent>> HandleAsync(Guid personId, PublicPhotoVariant variant, CancellationToken cancellationToken)
    {
        if (await people.FindAsync(personId, cancellationToken) is not var (person, exposure) || !exposure.Visible)
        {
            return RegistryErrors.PersonNotFound;
        }

        if (!exposure.ShowsPhoto || person.PhotoAssetId is not { } assetId)
        {
            return RegistryErrors.PhotoNotFound;
        }

        var opened = await media.OpenAsync(assetId, ToMediaVariant(variant), cancellationToken);
        return opened.IsFailure ? RegistryErrors.PhotoNotFound : opened;
    }

    private static MediaVariant ToMediaVariant(PublicPhotoVariant variant) => variant switch
    {
        PublicPhotoVariant.Thumbnail => MediaVariant.Thumbnail,
        PublicPhotoVariant.Medium => MediaVariant.Medium,
        PublicPhotoVariant.Large => MediaVariant.Large,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Not a photo variant."),
    };
}

/// <summary>A size of a public photo: 160, 640 or 1600 px on the longest side.</summary>
public enum PublicPhotoVariant
{
    Thumbnail,
    Medium,
    Large,
}
