using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// A person in the directory (S06 D22). <c>person.read</c> answers <c>INTERNAL</c> and <c>PUBLIC</c> fields only: the
/// birth year, <c>isMinor</c> and <c>ageStatus</c> on the tenant's date, never the date of birth (S06 D3, D13).
/// <c>isMinor</c> is true only for a known minor; a person without a date of birth is <c>UNKNOWN</c>.
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
/// removed. <c>version</c> is also the <c>ETag</c>; a change to an alias or an identifier changes it too (S06 D11).
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
