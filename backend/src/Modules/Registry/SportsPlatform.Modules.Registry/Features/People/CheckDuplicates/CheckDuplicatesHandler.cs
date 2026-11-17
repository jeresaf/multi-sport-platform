using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Infrastructure;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// The duplicate check's answer (S06 D5): the existing people who may be the same person, strongest first, at most 20,
/// and the token a create needs (S06 D6). It carries no <c>PRIVATE</c> value: a birth year, never a date of birth.
/// </summary>
public sealed record CheckDuplicatesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<DuplicateCandidateResponse> Candidates,
    [property: PersonalData(PersonalDataClass.Internal)] string CheckToken);

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

/// <summary>
/// Runs the duplicate check for the request's tenant (S06 D5) and issues its token (S06 D6). It saves nothing. The
/// details' own rules come first (<see cref="PersonDetailsRules"/>).
/// </summary>
public sealed class CheckDuplicatesHandler(
    RegistryDbContext context, DuplicateFinder finder, DuplicateCheckTokens tokens, TenantToday tenantToday)
{
    public async Task<(CheckDuplicatesResponse? Response, FieldRefusal? Refusal)> HandleAsync(
        CheckDuplicatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (PersonDetailsRules.Refuse(request, await tenantToday.GetAsync(cancellationToken)) is { } refusal)
        {
            return (null, refusal);
        }

        if (request.PersonId is { } personId
            && !await context.Set<Person>().AnyAsync(p => p.Id == personId, cancellationToken))
        {
            return (null, new(RegistryErrors.PersonNotFound));
        }

        var input = PersonDetailsRules.ToCheckInput(request, request.PersonId);
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
            ],
            tokens.Issue(input, candidates.Select(candidate => candidate.PersonId))), null);
    }
}
