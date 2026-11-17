using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People.CreatePerson;

/// <summary>
/// A new person (S06 D2, D6): the biodata, external identifiers, the duplicate check's token for the same details, and
/// the reason to proceed past the people the check found.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePersonRequest(
    string? FirstName,
    string? LastName,
    string? MiddleName = null,
    string? OtherNames = null,
    string? DisplayName = null,
    Gender? Gender = null,
    [property: PersonalData(PersonalDataClass.Private)] DateOnly? DateOfBirth = null,
    string? Nationality = null,
    string? SecondaryNationality = null,
    int? HeightCm = null,
    decimal? WeightKg = null,
    IReadOnlyList<NewExternalIdentifier>? ExternalIdentifiers = null,
    string? CheckToken = null,
    string? OverrideReason = null) : IPersonBiodataDetails
{
    public const int CheckTokenMaxLength = 4000;

    IReadOnlyList<IIdentifierDetails?>? IPersonDetails.ExternalIdentifiers => ExternalIdentifiers;
}

/// <summary>An external identifier of the new person: the authority's code, its value, and since when it is valid.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NewExternalIdentifier(string? Authority, string? Value, DateOnly? ValidFrom = null) : IIdentifierDetails;
