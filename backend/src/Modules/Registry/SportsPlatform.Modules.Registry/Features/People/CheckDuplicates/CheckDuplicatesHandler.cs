using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Infrastructure;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// The duplicate check's answer (S06 D5): the existing people who may be the same person, strongest first, at most 20.
/// It carries no <c>PRIVATE</c> value: a birth year, never a date of birth.
/// </summary>
public sealed record CheckDuplicatesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<DuplicateCandidateResponse> Candidates);

/// <summary>
/// A person who matched. <c>EXACT_IDENTIFIER</c> blocks creation; <c>LIKELY</c> and <c>POSSIBLE</c> need a reason to
/// proceed. <c>status</c> marks an archived, inactive or deceased person.
/// </summary>
public sealed record DuplicateCandidateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid PersonId,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Internal)] int? BirthYear,
    [property: PersonalData(PersonalDataClass.Internal)] PersonStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<DuplicateEvidenceResponse> Evidence);

/// <summary>
/// Why the person matched. <c>value</c>: the authority for <c>SAME_EXTERNAL_ID</c>, the whole percentage for
/// <c>NAME_SIMILARITY</c>, otherwise null.
/// </summary>
public sealed record DuplicateEvidenceResponse(
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateEvidenceCode Code,
    [property: PersonalData(PersonalDataClass.Internal)] string? Value);

/// <summary>A refusal of the check that belongs to one field of the request.</summary>
public sealed record CheckDuplicatesRefusal(Error Error, string? Field);

/// <summary>
/// Runs the duplicate check for the request's tenant (S06 D5). It saves nothing. A first or last name without a letter
/// or a digit has no name key (<c>PERSON_NAME_INVALID</c>); an authority naming a national identity document is not
/// collected (<c>PERSON_EXTERNAL_ID_AUTHORITY_RESERVED</c>, S06 D10).
/// </summary>
public sealed class CheckDuplicatesHandler(RegistryDbContext context, DuplicateFinder finder)
{
    public async Task<(CheckDuplicatesResponse? Response, CheckDuplicatesRefusal? Refusal)> HandleAsync(
        CheckDuplicatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lastNameKey = NameNormaliser.Normalise(request.LastName);
        if (NameNormaliser.Normalise(request.FirstName).Length == 0)
        {
            return (null, new(RegistryErrors.NameInvalid, "firstName"));
        }

        if (lastNameKey.Length == 0)
        {
            return (null, new(RegistryErrors.NameInvalid, "lastName"));
        }

        var identifiers = (request.ExternalIdentifiers ?? [])
            .Select(identifier => (Authority: identifier.Authority!, Value: identifier.Value!.Trim()))
            .ToList();
        var reserved = identifiers.FindIndex(identifier => ExternalIdentifier.ReservedAuthorities.Contains(identifier.Authority));
        if (reserved >= 0)
        {
            return (null, new(RegistryErrors.ExternalIdAuthorityReserved, $"externalIdentifiers[{reserved}].authority"));
        }

        if (request.PersonId is { } personId
            && !await context.Set<Person>().AnyAsync(p => p.Id == personId, cancellationToken))
        {
            return (null, new(RegistryErrors.PersonNotFound, null));
        }

        var input = new DuplicateCheckInput(
            NameNormaliser.NameKey(request.FirstName, request.MiddleName, request.LastName, request.OtherNames),
            lastNameKey,
            request.DateOfBirth,
            request.Nationality,
            identifiers,
            request.PersonId);
        var candidates = await finder.FindAsync(input, cancellationToken);
        return (new CheckDuplicatesResponse(
            [
                .. candidates.Select(candidate => new DuplicateCandidateResponse(
                    candidate.PersonId,
                    candidate.DisplayName,
                    candidate.BirthYear,
                    candidate.Status,
                    candidate.Match.Confidence,
                    [.. candidate.Match.Evidence.Select(evidence => new DuplicateEvidenceResponse(evidence.Code, evidence.Value))])),
            ]), null);
    }
}
