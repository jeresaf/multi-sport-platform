using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>
/// A team as the admin reads it (S07 D19). Every field but <c>aliases</c> (nicknames and spellings, <c>INTERNAL</c>),
/// <c>externalIdentifiers</c> (<c>INTERNAL</c>) and the version is one the public reads may show (D16): a public read must not reuse this DTO whole (review #130 R2). The
/// organisation and the home venue are summaries of the tenant's records, whatever their status now (null without
/// one). <c>nameSince</c> is when the current name began (D8); <c>formerNames</c> are the earlier ones with their dates,
/// newest first (<c>PUBLIC</c>, D16), and <c>aliases</c> the current nicknames and spellings (<c>INTERNAL</c>).
/// <c>externalIdentifiers</c> are all of the team's, ended ones included, by authority (as an organisation's; S07-T3d); a
/// change to one moves the version (S06 D11). Images join with S07-T4.
/// </summary>
public sealed record TeamResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] GenderCategory GenderCategory,
    [property: PersonalData(PersonalDataClass.Public)] AgeGroup AgeGroup,
    [property: PersonalData(PersonalDataClass.Public)] TeamLevel Level,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] DateOnly NameSince,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string? Code,
    [property: PersonalData(PersonalDataClass.Public)] string? Motto,
    [property: PersonalData(PersonalDataClass.Public)] string? PrimaryColour,
    [property: PersonalData(PersonalDataClass.Public)] string? SecondaryColour,
    [property: PersonalData(PersonalDataClass.Public)] string? TertiaryColour,
    [property: PersonalData(PersonalDataClass.Public)] int? FoundedYear,
    [property: PersonalData(PersonalDataClass.Public)] TeamOrganisationResponse? Organisation,
    [property: PersonalData(PersonalDataClass.Public)] TeamVenueResponse? HomeVenue,
    [property: PersonalData(PersonalDataClass.Public)] TeamStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<TeamFormerNameResponse> FormerNames,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<TeamAliasResponse> Aliases,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ExternalIdentifierResponse> ExternalIdentifiers,
    [property: PersonalData(PersonalDataClass.Internal)] uint Version)
{
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

/// <summary>
/// A name the team had before a rename (S07 D8), with the short name and code it went with and its first and last day:
/// <c>PUBLIC</c> (D16), so results of that time can show it.
/// </summary>
public sealed record TeamFormerNameResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string? Code,
    [property: PersonalData(PersonalDataClass.Public)] DateOnly ValidFrom,
    [property: PersonalData(PersonalDataClass.Public)] DateOnly ValidTo);

/// <summary>A current nickname or spelling of a team (S07 D10): <c>INTERNAL</c> (D16).</summary>
public sealed record TeamAliasResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] string Name,
    [property: PersonalData(PersonalDataClass.Internal)] AliasKind Kind);

/// <summary>The organisation a team belongs to (S07 D16: id, display name, slug), whatever its status now.</summary>
public sealed record TeamOrganisationResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug);

/// <summary>A team's home venue (S07 D16: id, name, slug), whatever the venue's status now.</summary>
public sealed record TeamVenueResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string Slug);

/// <summary>
/// A team that looks like the one checked (S07 D13): enough to recognise it and open it. Its variant and category are
/// the checked team's (only those are compared); <c>organisation</c> is its organisation, whatever its status now, or
/// null for an independent team (owner, review #127 Q1); <c>status</c> marks an archived one.
/// </summary>
public sealed record TeamDuplicateCandidateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid TeamId,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] GenderCategory GenderCategory,
    [property: PersonalData(PersonalDataClass.Public)] AgeGroup AgeGroup,
    [property: PersonalData(PersonalDataClass.Public)] TeamLevel Level,
    [property: PersonalData(PersonalDataClass.Public)] TeamOrganisationResponse? Organisation,
    [property: PersonalData(PersonalDataClass.Public)] TeamStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] DuplicateConfidence Confidence,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<TeamDuplicateEvidence> Evidence)
{
    public static TeamDuplicateCandidateResponse From(TeamDuplicateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(
            candidate.TeamId, candidate.Name, candidate.Variant, candidate.Category.Gender, candidate.Category.AgeGroup,
            candidate.Category.Level,
            candidate.Organisation is { } organisation
                ? new TeamOrganisationResponse(organisation.Id, organisation.DisplayName, organisation.Slug)
                : null,
            candidate.Status,
            candidate.Confidence,
            candidate.Evidence);
    }
}

/// <summary>
/// The Problem Details of a team create or update refused with <c>422</c> (S07 D13), for the OpenAPI document and the generated
/// client: <c>candidates</c> is present only with <c>TEAM_DUPLICATE_REVIEW_REQUIRED</c>, the teams the warning found,
/// strongest first.
/// </summary>
public sealed class TeamDuplicateReviewProblem : ProblemDetails
{
    [PersonalData(PersonalDataClass.Public)]
    public IReadOnlyList<TeamDuplicateCandidateResponse>? Candidates { get; init; }
}

/// <summary>
/// The audit metadata of a create or an update that ran the duplicate warning (S07 D13): the teams it
/// showed, by ID, with their confidence. The reason to proceed is the entry's reason.
/// </summary>
public sealed record TeamDuplicateReviewAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<TeamDuplicateCandidateAudit> Candidates);

public sealed record TeamDuplicateCandidateAudit(
    [property: PersonalData(PersonalDataClass.Internal)] Guid TeamId,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence);
