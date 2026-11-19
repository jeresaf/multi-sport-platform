using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>
/// A person on the tenant's public site (S06 D18): only <c>PUBLIC</c> fields. For a person who isn't an adult on the
/// tenant's date, the display name only, plus the photo with <c>PHOTO</c> consent (ADR-0015): <c>nationality</c> is null and
/// <c>athleteProfiles</c> is empty.
/// </summary>
public sealed record PublicPersonResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string? Nationality,
    [property: PersonalData(PersonalDataClass.Public)] PublicPersonPhotoResponse? Photo,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<PublicAthleteProfileResponse> AthleteProfiles);

/// <summary>
/// The photo's public routes (<c>GET /people/{personId}/photo?variant=</c>): stable paths, relative to the API's origin,
/// that check the person's exposure on every request.
/// </summary>
public sealed record PublicPersonPhotoResponse(
    [property: PersonalData(PersonalDataClass.Public)] string ThumbnailUrl,
    [property: PersonalData(PersonalDataClass.Public)] string MediumUrl,
    [property: PersonalData(PersonalDataClass.Public)] string LargeUrl);

/// <summary>An active athlete profile: the definition's codes (labels from <c>GET /sports/{variant}</c>, AR-29).</summary>
public sealed record PublicAthleteProfileResponse(
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] string? Position,
    [property: PersonalData(PersonalDataClass.Public)] string? PublicName);
