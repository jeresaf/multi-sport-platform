using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People.UpdatePerson;

/// <summary>
/// A person's whole biodata, replacing what is stored (S06 D8): a field left out is cleared. External identifiers and
/// aliases change through their own actions (owner, S06-T3 plan). <c>checkToken</c> and <c>overrideReason</c> are
/// needed only when the names, date of birth or nationalities change; <c>version</c>, when sent, must be the person's
/// current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdatePersonRequest(
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
    string? CheckToken = null,
    string? OverrideReason = null,
    uint? Version = null) : IPersonBiodataDetails
{
    // The update's check compares no identifiers: they aren't changed here.
    IReadOnlyList<IIdentifierDetails?>? IPersonDetails.ExternalIdentifiers => null;

    public PersonBiodata ToBiodata() => new(
        FirstName!, LastName!, MiddleName, OtherNames, DisplayName, Gender, DateOfBirth, Nationality, SecondaryNationality,
        HeightCm, WeightKg);
}
