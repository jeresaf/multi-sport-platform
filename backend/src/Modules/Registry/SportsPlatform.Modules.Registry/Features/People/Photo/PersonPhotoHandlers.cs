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

        var used = StorageQuota.WholeGigabytes(await media.GetStoredBytesAsync(cancellationToken));
        var quota = await entitlements.CheckQuotaAsync(StorageQuota.Code, used, cancellationToken);
        if (quota.IsFailure)
        {
            var limit = await entitlements.GetLimitAsync(StorageQuota.Code, cancellationToken);
            return new(quota.Error, Detail: $"Your plan allows {limit} GB of storage.");
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
            await PersonPhotoAssets.RemoveAsync(media, logger, previous);
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

        await PersonPhotoAssets.RemoveAsync(media, logger, removed.Value);
        return null;
    }
}

/// <summary>The audit metadata of a photo change: the new and the replaced asset by ID.</summary>
public sealed record PersonPhotoAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? AssetId,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ReplacedAssetId);

internal static partial class PersonPhotoAssets
{
    /// <summary>
    /// Marks the asset the person no longer names <c>REMOVED</c>. The person is saved already, so a failure doesn't fail
    /// the request: the asset stays active and unreferenced (still counted toward storage) and is logged for the
    /// retention job (Later). Not cancelled with the request, for the same reason.
    /// </summary>
    public static async Task RemoveAsync(IMediaAssets media, ILogger logger, Guid assetId)
    {
        try
        {
            var removed = await media.RemoveAsync(assetId, CancellationToken.None);
            if (removed.IsFailure)
            {
                LogNotRemoved(logger, assetId, removed.Error.Code);
            }
        }
#pragma warning disable CA1031 // The person's change is saved: any failure here is logged, not answered.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogRemoveFailed(logger, exception, assetId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Photo asset {AssetId}, no longer named by its person, was not removed: {ErrorCode}")]
    private static partial void LogNotRemoved(ILogger logger, Guid assetId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removing photo asset {AssetId}, no longer named by its person, failed")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, Guid assetId);
}
