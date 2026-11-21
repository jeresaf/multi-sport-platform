using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// A person in the directory (S06 D22). <c>person.read</c> answers <c>INTERNAL</c> and <c>PUBLIC</c> fields only: the
/// birth year, <c>isMinor</c> and <c>ageStatus</c> on the tenant's date, never the date of birth (S06 D3, D13).
/// <c>isMinor</c> is true unless the person is known to be an adult: a person without a date of birth
/// (<c>UNKNOWN</c>) is treated as a minor.
/// </summary>
public sealed record PersonListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Internal)] string FirstName,
    [property: PersonalData(PersonalDataClass.Internal)] string LastName,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] int? BirthYear,
    [property: PersonalData(PersonalDataClass.Internal)] bool IsMinor,
    [property: PersonalData(PersonalDataClass.Internal)] AgeStatus AgeStatus,
    [property: PersonalData(PersonalDataClass.Public)] string? Nationality);

/// <summary>
/// A person's record as <c>person.read</c> shows it (S06 D3, D22): no <c>PRIVATE</c> field. Aliases are the ones not
/// removed; athlete profiles are all of them, archived ones included, by variant; staff and official profiles all of
/// them, archived ones included, oldest first. <c>photo</c> holds short-lived URLs (S06 D17), null without a photo.
/// <c>version</c> is also the <c>ETag</c>;
/// a change to an alias, an identifier or a profile changes it too (S06 D11).
/// </summary>
public sealed record PersonResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] string FirstName,
    [property: PersonalData(PersonalDataClass.Internal)] string? MiddleName,
    [property: PersonalData(PersonalDataClass.Internal)] string LastName,
    [property: PersonalData(PersonalDataClass.Internal)] string? OtherNames,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Internal)] Gender? Gender,
    [property: PersonalData(PersonalDataClass.Internal)] int? BirthYear,
    [property: PersonalData(PersonalDataClass.Internal)] bool IsMinor,
    [property: PersonalData(PersonalDataClass.Internal)] AgeStatus AgeStatus,
    [property: PersonalData(PersonalDataClass.Public)] string? Nationality,
    [property: PersonalData(PersonalDataClass.Public)] string? SecondaryNationality,
    [property: PersonalData(PersonalDataClass.Internal)] int? HeightCm,
    [property: PersonalData(PersonalDataClass.Internal)] decimal? WeightKg,
    [property: PersonalData(PersonalDataClass.Internal)] bool PublicProfileEnabled,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] PersonSource Source,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<PersonAliasResponse> Aliases,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ExternalIdentifierResponse> ExternalIdentifiers,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<AthleteProfileResponse> AthleteProfiles,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<StaffProfileResponse> StaffProfiles,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<OfficialProfileResponse> OfficialProfiles,
    [property: PersonalData(PersonalDataClass.Internal)] PersonPhotoResponse? Photo,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ConsentScope> ActiveConsentScopes,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ConsentResponse> Consents,
    [property: PersonalData(PersonalDataClass.Internal)] uint Version)
{
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

/// <summary>
/// A person's photo as signed URLs, one per size (S06 D17): 160, 640 and 1600 px on the longest side, WebP. They work
/// until <c>expiresAt</c> (ten minutes with the local storage); read the person again for new ones. Never cache them.
/// </summary>
public sealed record PersonPhotoResponse(
    [property: PersonalData(PersonalDataClass.Internal)] string ThumbnailUrl,
    [property: PersonalData(PersonalDataClass.Internal)] string MediumUrl,
    [property: PersonalData(PersonalDataClass.Internal)] string LargeUrl,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset ExpiresAt);

public sealed record PersonAliasResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] string Name,
    [property: PersonalData(PersonalDataClass.Internal)] AliasKind Kind);

/// <summary>
/// A sport variant the person plays (S06 D12): codes of the variant's definition (its labels come from
/// <c>GET /sports/{variant}</c>, AR-29). <c>publicName</c> as recorded: null shows the display name.
/// </summary>
public sealed record AthleteProfileResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] string? PrimaryPosition,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<string> OtherPositions,
    [property: PersonalData(PersonalDataClass.Internal)] DominantSide? DominantSide,
    [property: PersonalData(PersonalDataClass.Public)] string? PublicName,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus Status);

/// <summary>
/// A person's work as staff (S06 D12): what they do, by variant and role (codes of the variant's definition; labels from
/// <c>GET /sports/{variant}</c>, AR-29).
/// </summary>
public sealed record StaffProfileResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<StaffCapabilityResponse> Capabilities,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus Status);

/// <summary>One thing a staff member does: a variant, a staff role of its definition and the qualification recorded.</summary>
public sealed record StaffCapabilityResponse(
    [property: PersonalData(PersonalDataClass.Internal)] string Variant,
    [property: PersonalData(PersonalDataClass.Internal)] string Role,
    [property: PersonalData(PersonalDataClass.Internal)] string? Qualification);

/// <summary>
/// A person's work as an official (S06 D12): what they officiate as, by variant and role (codes of the variant's
/// definition; labels from <c>GET /sports/{variant}</c>, AR-29).
/// </summary>
public sealed record OfficialProfileResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<OfficialCapabilityResponse> Capabilities,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus Status);

/// <summary>
/// One thing an official does: a variant, an official role of its definition, the grade and licence recorded.
/// <c>licenceExpired</c> is true when <c>licenceValidUntil</c> is before the tenant's today (a licence is valid through
/// its last day; without a date it never expires). Nothing is refused for an expired licence in S06.
/// </summary>
public sealed record OfficialCapabilityResponse(
    [property: PersonalData(PersonalDataClass.Internal)] string Variant,
    [property: PersonalData(PersonalDataClass.Internal)] string Role,
    [property: PersonalData(PersonalDataClass.Internal)] string? Grade,
    [property: PersonalData(PersonalDataClass.Internal)] string? LicenceNumber,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? LicenceValidUntil,
    [property: PersonalData(PersonalDataClass.Internal)] bool LicenceExpired);

/// <summary>An identifier another authority gave the person or venue; one with <c>validTo</c> has ended (S06 D10, S07 D10).</summary>
public sealed record ExternalIdentifierResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] string Authority,
    [property: PersonalData(PersonalDataClass.Internal)] string Value,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? ValidFrom,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? ValidTo);

/// <summary>
/// A guardian's consent as <c>person.read</c> shows it (S06 D3, D14): its scopes and dates, never the guardian's
/// details or the evidence (those are in the private read). A withdrawn consent stays, with when and why;
/// <c>replacesConsentId</c> names the consent a partial withdrawal closed to record this one.
/// </summary>
public sealed record ConsentResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ConsentScope> Scopes,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly GivenOn,
    [property: PersonalData(PersonalDataClass.Internal)] ConsentStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] bool HasEvidence,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset RecordedAt,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset? WithdrawnAt,
    [property: PersonalData(PersonalDataClass.Internal)] string? WithdrawalReason,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ReplacesConsentId);

/// <summary>
/// A person's <c>PRIVATE</c> data, for <c>person.read_private</c> (S06 D3): the full date of birth, and the guardian
/// and the evidence of every consent, withdrawn ones included (matched to <c>consents</c> of the person read by ID).
/// </summary>
public sealed record PersonPrivateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] DateOnly? DateOfBirth,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ConsentPrivateResponse> Consents);

/// <summary>A consent's guardian and evidence (S06 D14).</summary>
public sealed record ConsentPrivateResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] string GuardianName,
    [property: PersonalData(PersonalDataClass.Private)] GuardianRelationship Relationship,
    [property: PersonalData(PersonalDataClass.Private)] string Contact,
    [property: PersonalData(PersonalDataClass.Private)] ConsentEvidenceResponse? Evidence);

/// <summary>
/// The evidence's signed URL (S06 D17): it downloads the file until <c>expiresAt</c> (ten minutes with the local
/// storage); read again for a new one. Never cache it.
/// </summary>
public sealed record ConsentEvidenceResponse(
    [property: PersonalData(PersonalDataClass.Private)] string Url,
    [property: PersonalData(PersonalDataClass.Private)] DateTimeOffset ExpiresAt);

/// <summary>One row of a person's status history (S06 D9): <c>fromStatus</c> is null for the row written at creation.</summary>
public sealed record PersonStatusChangeResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus? FromStatus,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus ToStatus,
    [property: PersonalData(PersonalDataClass.Internal)] string? Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset OccurredAt);
