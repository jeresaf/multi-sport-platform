using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>
/// The public person read (S06 D18): <c>PERSON_NOT_FOUND</c> unless <see cref="Domain.People.PublicExposure"/> makes the
/// person visible. An adult shows nationality, the photo and active athlete profiles; anyone else the display name and,
/// with <c>PHOTO</c> consent, the photo.
/// </summary>
public sealed class GetPublicPersonHandler(RegistryDbContext context, PublicPeople people)
{
    public async Task<Result<PublicPersonResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (await people.FindAsync(personId, cancellationToken) is not var (person, exposure) || !exposure.Visible)
        {
            return RegistryErrors.PersonNotFound;
        }

        IReadOnlyList<PublicAthleteProfileResponse> profiles = [];
        if (exposure.ShowsDetails)
        {
            profiles = await context.Set<AthleteProfile>().AsNoTracking()
                .Where(a => a.PersonId == personId && a.Status == ProfileStatus.Active)
                .OrderBy(a => a.Variant).ThenBy(a => a.Id)
                .Select(a => new PublicAthleteProfileResponse(a.Variant, a.PrimaryPosition, a.PublicName))
                .ToListAsync(cancellationToken);
        }

        var photo = exposure.ShowsPhoto && person.PhotoAssetId is not null
            ? new PublicPersonPhotoResponse(
                PublicPersonEndpoints.PhotoPath(personId, PublicPhotoVariant.Thumbnail),
                PublicPersonEndpoints.PhotoPath(personId, PublicPhotoVariant.Medium),
                PublicPersonEndpoints.PhotoPath(personId, PublicPhotoVariant.Large))
            : null;
        return Result.Success(new PublicPersonResponse(
            person.Id, person.DisplayName, exposure.ShowsDetails ? person.Nationality : null, photo, profiles));
    }
}
