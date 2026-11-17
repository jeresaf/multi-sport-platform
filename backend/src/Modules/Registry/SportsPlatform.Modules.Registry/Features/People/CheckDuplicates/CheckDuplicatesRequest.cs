using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// The person to check (S06 D5): the names, date of birth, nationality and external identifiers a create or an update
/// would save. <c>personId</c> names the person being changed (S06 D8): that person is left out, and so is everyone
/// already decided "not the same person" as them (S06 D7).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckDuplicatesRequest(
    string? FirstName,
    string? LastName,
    string? MiddleName = null,
    string? OtherNames = null,
    [property: PersonalData(PersonalDataClass.Private)] DateOnly? DateOfBirth = null,
    string? Nationality = null,
    IReadOnlyList<CandidateIdentifier>? ExternalIdentifiers = null,
    Guid? PersonId = null)
{
    public const int MaxIdentifiers = 10;
}

/// <summary>An external identifier of the person to check: the authority's code and the value it gave.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CandidateIdentifier(string? Authority, string? Value);
