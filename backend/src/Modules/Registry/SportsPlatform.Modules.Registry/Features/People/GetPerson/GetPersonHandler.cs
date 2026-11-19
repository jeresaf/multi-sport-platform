using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPerson;

/// <summary>
/// One person of the request's tenant, whatever its status; another tenant's person is not found (RLS). The date of
/// birth is read to compute the birth year and the age status on the tenant's date, and goes no further (S06 D3, D13).
/// The photo is answered as signed URLs from Platform's media (S06 D17).
/// </summary>
public sealed partial class GetPersonHandler(
    RegistryDbContext context, TenantToday tenantToday, IMediaAssets media, ILogger<GetPersonHandler> logger)
{
    public async Task<Result<PersonResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await context.Set<Person>().AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(p => new
            {
                Person = p,
                Aliases = p.Aliases.Where(a => a.RemovedAt == null).OrderBy(a => a.Id)
                    .Select(a => new PersonAliasResponse(a.Id, a.Name, a.Kind)).ToList(),
                Version = EF.Property<uint>(p, AuditColumns.Version),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (person is null)
        {
            return RegistryErrors.PersonNotFound;
        }

        var identifiers = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Person && i.EntityId == personId)
            .OrderBy(i => i.Authority).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentifierResponse(i.Id, i.Authority, i.Value, i.ValidFrom, i.ValidTo))
            .ToListAsync(cancellationToken);

        var profiles = await context.Set<AthleteProfile>().AsNoTracking()
            .Where(a => a.PersonId == personId)
            .OrderBy(a => a.Variant).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var staffProfiles = await context.Set<StaffProfile>().AsNoTracking()
            .Include(s => s.Capabilities)
            .Where(s => s.PersonId == personId)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var officialProfiles = await context.Set<OfficialProfile>().AsNoTracking()
            .Include(o => o.Capabilities)
            .Where(o => o.PersonId == personId)
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken);

        var consents = await context.Set<GuardianConsent>().AsNoTracking()
            .Where(c => c.PersonId == personId)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var p = person.Person;
        var photo = p.PhotoAssetId is { } photoAssetId ? await PhotoAsync(p.Id, photoAssetId, cancellationToken) : null;
        var today = await tenantToday.GetAsync(cancellationToken);
        var ageStatus = Age.StatusOn(p.DateOfBirth, today);
        return Result.Success(new PersonResponse(
            p.Id, p.FirstName, p.MiddleName, p.LastName, p.OtherNames, p.DisplayName, p.Gender, p.DateOfBirth?.Year,
            Age.IsTreatedAsMinor(ageStatus), ageStatus, p.Nationality, p.SecondaryNationality, p.HeightCm, p.WeightKg,
            p.PublicProfileEnabled, p.Status, p.Source, person.Aliases, identifiers,
            [.. profiles.Select(a => new AthleteProfileResponse(
                a.Id, a.Variant, a.PrimaryPosition, a.OtherPositions, a.DominantSide, a.PublicName, a.Status))],
            [.. staffProfiles.Select(s => new StaffProfileResponse(
                s.Id,
                [.. s.Capabilities.OrderBy(c => c.Variant, StringComparer.Ordinal).ThenBy(c => c.Role, StringComparer.Ordinal)
                    .Select(c => new StaffCapabilityResponse(c.Variant, c.Role, c.Qualification))],
                s.Status))],
            [.. officialProfiles.Select(o => new OfficialProfileResponse(
                o.Id,
                [.. o.Capabilities.OrderBy(c => c.Variant, StringComparer.Ordinal).ThenBy(c => c.Role, StringComparer.Ordinal)
                    .Select(c => new OfficialCapabilityResponse(
                        c.Variant, c.Role, c.Grade, c.LicenceNumber, c.LicenceValidUntil, c.LicenceExpiredOn(today)))],
                o.Status))],
            photo,
            GuardianConsent.ActiveScopes(consents),
            [.. consents.Select(c => new ConsentResponse(
                c.Id, c.Scopes, c.GivenOn, c.Status, c.EvidenceAssetId is not null, c.RecordedAt, c.WithdrawnAt,
                c.WithdrawalReason, c.ReplacesConsentId))],
            person.Version));
    }

    /// <summary>
    /// The photo's URLs, or null when Platform doesn't serve them all: logged, since a person names only an active asset
    /// (a read racing a photo's replacement can also see the old one removed). The read still answers.
    /// </summary>
    private async Task<PersonPhotoResponse?> PhotoAsync(Guid personId, Guid assetId, CancellationToken cancellationToken)
    {
        var thumbnail = await media.GetSignedUrlAsync(assetId, MediaVariant.Thumbnail, cancellationToken);
        var medium = await media.GetSignedUrlAsync(assetId, MediaVariant.Medium, cancellationToken);
        var large = await media.GetSignedUrlAsync(assetId, MediaVariant.Large, cancellationToken);
        if (thumbnail.IsFailure || medium.IsFailure || large.IsFailure)
        {
            var error = new[] { thumbnail.Error, medium.Error, large.Error }.First(e => e is not null)!;
            LogPhotoNotServed(personId, assetId, error.Code);
            return null;
        }

        var expiresAt = new[] { thumbnail.Value.ExpiresAt, medium.Value.ExpiresAt, large.Value.ExpiresAt }.Min();
        return new(thumbnail.Value.Url, medium.Value.Url, large.Value.Url, expiresAt);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Person {PersonId} names photo asset {AssetId}, which Platform doesn't serve: {ErrorCode}")]
    private partial void LogPhotoNotServed(Guid personId, Guid assetId, string errorCode);
}
