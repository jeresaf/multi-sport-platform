using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Media;

/// <summary>
/// What the Registry's uploads share (S06 D19, D20; S07 D15): the one-file form, the <c>STORAGE_GB</c> check before
/// storing, and marking a replaced asset <c>REMOVED</c> after the record's save.
/// </summary>
internal static partial class RegistryMedia
{
    /// <summary>The form field that carries the file.</summary>
    public const string FileField = "file";

    /// <summary>What a logo or badge upload's description says about the file (S07 D15), for <paramref name="image"/>.</summary>
    public static string ImageUploadNote(string image) =>
        $"Uploads the {image} as `multipart/form-data` with one file in the field `{FileField}` and nothing else " +
        "(VALIDATION_INVALID_REQUEST otherwise). The file's bytes decide its type, not the content type sent: JPEG, PNG or " +
        "WebP up to 2 MB (MEDIA_TYPE_NOT_ALLOWED, MEDIA_TOO_LARGE; no SVG), at least 64 px on the shorter side " +
        "(MEDIA_IMAGE_TOO_SMALL; an image that can't be decoded is MEDIA_IMAGE_UNREADABLE). It is stored as a public WebP " +
        $"image at 160 and 640 px, transparency kept, and replaces the {image} it had. At the plan's storage limit the " +
        "upload is ENTITLEMENT_QUOTA_EXCEEDED (\"Your plan allows N GB of storage.\"). ";

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
    /// Marks an asset its record (a person, an organisation, a team) no longer names <c>REMOVED</c>. The record is saved
    /// already, so a failure doesn't fail
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
#pragma warning disable CA1031 // The record's change is saved: any failure here is logged, not answered.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogRemoveFailed(logger, exception, assetId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Media asset {AssetId}, no longer named by its record, was not removed: {ErrorCode}")]
    private static partial void LogNotRemoved(ILogger logger, Guid assetId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removing media asset {AssetId}, no longer named by its record, failed")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, Guid assetId);
}
