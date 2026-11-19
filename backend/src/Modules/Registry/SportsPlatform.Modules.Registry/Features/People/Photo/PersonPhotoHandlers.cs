using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.People.Photo;

/// <summary>
/// Sets a person's photo (S06 D19): refused on an archived or deceased person (D9) and at the <c>STORAGE_GB</c> limit
/// (D20, checked before storing, without a lock: two uploads at the limit may both pass). Platform checks the file by
/// its bytes and stores it as WebP variants; the person then names the new asset, and only after that save is the old
/// asset marked <c>REMOVED</c>, so a failure leaves an unreferenced active asset, never a person naming a removed one.
/// </summary>
public sealed class SetPersonPhotoHandler(
    RegistryDbContext context,
    IMediaAssets media,
    IEntitlements entitlements,
    AuditScope audit,
    ILogger<SetPersonPhotoHandler> logger)
{
    public async Task<FieldRefusal?> HandleAsync(Guid personId, Stream file, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, version: null, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var accepts = person.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        if (await PersonMedia.CheckStorageAsync(media, entitlements, cancellationToken) is { } overQuota)
        {
            return overQuota;
        }

        var stored = await media.StoreAsync(file, MediaPurpose.PersonPhoto, cancellationToken);
        if (stored.IsFailure)
        {
            return new(stored.Error, PersonPhotoEndpoints.FileField);
        }

        var set = person.SetPhoto(stored.Value);
        if (set.IsFailure)
        {
            return new(set.Error);
        }

        audit.Describe(metadata: new PersonPhotoAuditMetadata(stored.Value, set.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        if (set.Value is { } previous)
        {
            await PersonMedia.RemoveUnreferencedAsync(media, logger, previous);
        }

        return null;
    }
}

/// <summary>
/// Removes a person's photo in any status (a correction, S06 D19): <c>PERSON_PHOTO_NOT_FOUND</c> without one (owner,
/// S06-T5b plan). The asset is marked <c>REMOVED</c> after the person's save; its files stay until a retention job.
/// </summary>
public sealed class RemovePersonPhotoHandler(
    RegistryDbContext context, IMediaAssets media, AuditScope audit, ILogger<RemovePersonPhotoHandler> logger)
{
    public async Task<FieldRefusal?> HandleAsync(Guid personId, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, version: null, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var removed = person.RemovePhoto();
        if (removed.IsFailure)
        {
            return new(removed.Error);
        }

        audit.Describe(metadata: new PersonPhotoAuditMetadata(null, removed.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        await PersonMedia.RemoveUnreferencedAsync(media, logger, removed.Value);
        return null;
    }
}

/// <summary>The audit metadata of a photo change: the new and the replaced asset by ID.</summary>
public sealed record PersonPhotoAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? AssetId,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ReplacedAssetId);
