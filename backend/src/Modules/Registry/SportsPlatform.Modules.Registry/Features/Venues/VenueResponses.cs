using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary>
/// A venue as the admin reads it (S07 D19). Every field is one the public reads may show (D16) but the version.
/// Surfaces and variants keep the order they were sent in; variants are codes, labelled from the definitions (AR-29).
/// </summary>
public sealed record VenueResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] VenueType Type,
    [property: PersonalData(PersonalDataClass.Public)] string? AddressLine,
    [property: PersonalData(PersonalDataClass.Public)] string City,
    [property: PersonalData(PersonalDataClass.Public)] string? Region,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] decimal? Latitude,
    [property: PersonalData(PersonalDataClass.Public)] decimal? Longitude,
    [property: PersonalData(PersonalDataClass.Public)] string TimeZone,
    [property: PersonalData(PersonalDataClass.Public)] int? Capacity,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<VenueSurface> Surfaces,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<string> SupportedVariants,
    [property: PersonalData(PersonalDataClass.Public)] VenueStatus Status,
    [property: PersonalData(PersonalDataClass.Internal)] uint Version)
{
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

/// <summary>
/// A venue that looks like the one checked (S07 D13): enough to recognise it and open it. <c>status</c> marks an
/// archived one.
/// </summary>
public sealed record VenueDuplicateCandidateResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid VenueId,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] VenueType Type,
    [property: PersonalData(PersonalDataClass.Public)] string City,
    [property: PersonalData(PersonalDataClass.Public)] VenueStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] DuplicateConfidence Confidence,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<VenueDuplicateEvidence> Evidence)
{
    public static VenueDuplicateCandidateResponse From(VenueDuplicateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(candidate.VenueId, candidate.Name, candidate.Type, candidate.City, candidate.Status, candidate.Confidence, candidate.Evidence);
    }
}

/// <summary>
/// The audit metadata of a create (or, in S07-T1b, an update) that ran the duplicate warning (S07 D13): the venues it
/// showed, by ID, with their confidence. The reason to proceed is the entry's reason.
/// </summary>
public sealed record VenueDuplicateReviewAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<VenueDuplicateCandidateAudit> Candidates);

public sealed record VenueDuplicateCandidateAudit(
    [property: PersonalData(PersonalDataClass.Internal)] Guid VenueId,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence);
