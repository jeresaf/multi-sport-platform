using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;

namespace SportsPlatform.Modules.Registry.Features.Organisations;

/// <summary>
/// An organisation as the admin reads it (S07 D19). The legal name, the body's own email and phone and the version are
/// <c>INTERNAL</c>; every other field is one the public reads may show (D16). The primary venue is a summary of the
/// tenant's venue (null without one). <c>parent</c> is the current parent in any status (null without one); <c>children</c>
/// are the organisations whose current parent this is, archived ones left out (owner, S07-T2c-2 plan), sorted by display
/// name; the directory's <c>parentId</c> pages them with filters. The version doesn't move when a child leaves (S07-T2c-1),
/// so it is no freshness marker for <c>children</c>. Teams, aliases, identifiers and the logo join with S07-T2d, T3 and T4.
/// </summary>
public sealed record OrganisationResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationType Type,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Internal)] string? LegalName,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] string? City,
    [property: PersonalData(PersonalDataClass.Public)] int? FoundedYear,
    [property: PersonalData(PersonalDataClass.Public)] string? Website,
    [property: PersonalData(PersonalDataClass.Internal)] string? Email,
    [property: PersonalData(PersonalDataClass.Internal)] string? Phone,
    [property: PersonalData(PersonalDataClass.Public)] string? Description,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationVenueResponse? PrimaryVenue,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationSummaryResponse? Parent,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<OrganisationSummaryResponse> Children,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] uint Version)
{
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

/// <summary>A venue an organisation points at (S07 D16: id, name, slug), whatever the venue's status now.</summary>
public sealed record OrganisationVenueResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string Slug);

/// <summary>
/// An organisation another points at, as its parent or as one of its children (S07 D16: id, display name, slug), with its
/// status now.
/// </summary>
public sealed record OrganisationSummaryResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status);

/// <summary>
/// An organisation that looks like the one checked (S07 D13): enough to recognise it and open it. <c>status</c> marks an
/// archived one.
/// </summary>
public sealed record OrganisationDuplicateCandidateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid OrganisationId,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationType Type,
    [property: PersonalData(PersonalDataClass.Public)] string? City,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] DuplicateConfidence Confidence,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<OrganisationDuplicateEvidence> Evidence)
{
    public static OrganisationDuplicateCandidateResponse From(OrganisationDuplicateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(
            candidate.OrganisationId, candidate.DisplayName, candidate.Type, candidate.City, candidate.Status, candidate.Confidence,
            candidate.Evidence);
    }
}

/// <summary>
/// The Problem Details of an organisation create refused with <c>422</c> (S07 D13), for the OpenAPI document and the
/// generated client: <c>candidates</c> is present only with <c>ORGANISATION_DUPLICATE_REVIEW_REQUIRED</c>, the
/// organisations the warning found, strongest first.
/// </summary>
public sealed class OrganisationDuplicateReviewProblem : ProblemDetails
{
    [PersonalData(PersonalDataClass.Public)]
    public IReadOnlyList<OrganisationDuplicateCandidateResponse>? Candidates { get; init; }
}

/// <summary>
/// The audit metadata of a create (or, in S07-T2b, an update) that ran the duplicate warning (S07 D13): the
/// organisations it showed, by ID, with their confidence. The reason to proceed is the entry's reason.
/// </summary>
public sealed record OrganisationDuplicateReviewAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<OrganisationDuplicateCandidateAudit> Candidates);

public sealed record OrganisationDuplicateCandidateAudit(
    [property: PersonalData(PersonalDataClass.Internal)] Guid OrganisationId,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence);
