using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;

/// <summary>
/// A person's <c>PRIVATE</c> data (S06 D3, D14, D21): the date of birth and every consent's guardian and evidence (a
/// signed URL from Platform's media, S06 D17). Saves nothing, so the Host writes one audit entry per read after the
/// response; the entry names the person by ID and carries no metadata.
/// </summary>
public sealed partial class GetPersonPrivateHandler(
    RegistryDbContext context, IMediaAssets media, AuditScope audit, ILogger<GetPersonPrivateHandler> logger)
{
    public async Task<Result<PersonPrivateResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        audit.Describe(PeopleEndpoints.EntityType, personId);
        var person = await context.Set<Person>().AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(p => new { p.Id, p.DateOfBirth })
            .SingleOrDefaultAsync(cancellationToken);
        if (person is null)
        {
            return RegistryErrors.PersonNotFound;
        }

        var consents = await context.Set<GuardianConsent>().AsNoTracking()
            .Where(c => c.PersonId == personId)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var answered = new List<ConsentPrivateResponse>(consents.Count);
        foreach (var consent in consents)
        {
            var evidence = consent.EvidenceAssetId is { } assetId
                ? await EvidenceAsync(consent.Id, assetId, cancellationToken)
                : null;
            answered.Add(new(consent.Id, consent.GuardianName, consent.Relationship, consent.Contact, evidence));
        }

        return Result.Success(new PersonPrivateResponse(person.Id, person.DateOfBirth, answered));
    }

    /// <summary>
    /// The evidence's URL, or null when Platform doesn't serve it: logged, since a consent names only an active asset (a
    /// read racing a replacement can see the old one removed). The read still answers.
    /// </summary>
    private async Task<ConsentEvidenceResponse?> EvidenceAsync(Guid consentId, Guid assetId, CancellationToken cancellationToken)
    {
        var url = await media.GetSignedUrlAsync(assetId, MediaVariant.Original, cancellationToken);
        if (url.IsFailure)
        {
            LogEvidenceNotServed(consentId, assetId, url.Error.Code);
            return null;
        }

        return new(url.Value.Url, url.Value.ExpiresAt);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consent {ConsentId} names evidence asset {AssetId}, which Platform doesn't serve: {ErrorCode}")]
    private partial void LogEvidenceNotServed(Guid consentId, Guid assetId, string errorCode);
}
