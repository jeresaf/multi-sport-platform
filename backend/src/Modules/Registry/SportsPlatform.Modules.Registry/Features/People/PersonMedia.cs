using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// What a person's uploads share (S06 D19, D20): the one-file form, the <c>STORAGE_GB</c> check before storing, and
/// marking a replaced asset <c>REMOVED</c> after the person's save.
/// </summary>
internal static partial class PersonMedia
{
    /// <summary>The form field that carries the file.</summary>
    public const string FileField = "file";

    /// <summary>
    /// The refusal of a form that isn't one file in <see cref="FileField"/> and nothing else. <paramref name="file"/> is
    /// bound as optional, so a missing or misnamed file is refused here with the field, not by the binder; it binds the
    /// first file of the field, so anything else in the form is refused too, not ignored.
    /// </summary>
    public static FieldRefusal? RefuseUnlessOneFile(IFormFile? file, HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var form = request.Form;
        return file is null || form.Files.Count != 1 || form.Count != 0
            ? new(CommonErrors.InvalidRequest, FileField, $"Send one file in the form field '{FileField}' and nothing else.")
            : null;
    }

    /// <summary>
    /// <c>ENTITLEMENT_QUOTA_EXCEEDED</c> ("Your plan allows N GB of storage.") when the tenant's stored bytes, in whole GB
    /// as Tenancy counts them, are at the plan's limit (S06 D20). No lock: two uploads at the limit may both pass.
    /// </summary>
    public static async Task<FieldRefusal?> CheckStorageAsync(
        IMediaAssets media, IEntitlements entitlements, CancellationToken cancellationToken)
    {
        var used = StorageQuota.WholeGigabytes(await media.GetStoredBytesAsync(cancellationToken));
        var quota = await entitlements.CheckQuotaAsync(StorageQuota.Code, used, cancellationToken);
        if (quota.IsSuccess)
        {
            return null;
        }

        var limit = await entitlements.GetLimitAsync(StorageQuota.Code, cancellationToken);
        return new(quota.Error, Detail: $"Your plan allows {limit} GB of storage.");
    }

    /// <summary>
    /// Marks an asset the person no longer names <c>REMOVED</c>. The person is saved already, so a failure doesn't fail
    /// the request: the asset stays active and unreferenced (still counted toward storage) and is logged for the
    /// retention job (Later). Not cancelled with the request, for the same reason.
    /// </summary>
    public static async Task RemoveUnreferencedAsync(IMediaAssets media, ILogger logger, Guid assetId)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Media asset {AssetId}, no longer named by its person, was not removed: {ErrorCode}")]
    private static partial void LogNotRemoved(ILogger logger, Guid assetId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removing media asset {AssetId}, no longer named by its person, failed")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, Guid assetId);
}
