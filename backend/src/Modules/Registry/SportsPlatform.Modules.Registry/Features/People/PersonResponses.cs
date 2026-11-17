using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
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
/// them, archived ones included, oldest first. <c>version</c> is also the <c>ETag</c>;
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
    [property: PersonalData(PersonalDataClass.Internal)] uint Version)
{
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

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

/// <summary>An identifier another authority gave the person; one with <c>validTo</c> has ended (S06 D10).</summary>
public sealed record ExternalIdentifierResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] string Authority,
    [property: PersonalData(PersonalDataClass.Internal)] string Value,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? ValidFrom,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? ValidTo);

/// <summary>A person's <c>PRIVATE</c> data, for <c>person.read_private</c> (S06 D3): the full date of birth.</summary>
public sealed record PersonPrivateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] DateOnly? DateOfBirth);

/// <summary>One row of a person's status history (S06 D9): <c>fromStatus</c> is null for the row written at creation.</summary>
public sealed record PersonStatusChangeResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus? FromStatus,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus ToStatus,
    [property: PersonalData(PersonalDataClass.Internal)] string? Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset OccurredAt);
