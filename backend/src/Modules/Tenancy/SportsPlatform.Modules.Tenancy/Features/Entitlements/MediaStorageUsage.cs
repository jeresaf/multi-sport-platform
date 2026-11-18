using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements;

/// <summary>
/// The scope's tenant's media storage for <c>STORAGE_GB</c> (S06 D20, Q36): Platform counts the bytes of every variant of
/// the tenant's active assets, and Tenancy registers the counter, because Platform depends on no other module. Whole
/// gigabytes (2³⁰ bytes), rounded down, so the usage reads "0 of 10 GB" until the first full gigabyte. The uploading
/// module checks the quota before storing (<c>IEntitlements.CheckQuotaAsync</c>).
/// </summary>
public sealed class MediaStorageUsage(IMediaAssets media) : IQuotaUsage
{
    public static readonly string Code = TenancyEntitlements.StorageGb.Code;

    public const long BytesPerGigabyte = 1L << 30;

    public async Task<int> CountAsync(CancellationToken cancellationToken) =>
        ToWholeGigabytes(await media.GetStoredBytesAsync(cancellationToken));

    public static int ToWholeGigabytes(long bytes) => (int)Math.Min(int.MaxValue, Math.Max(0, bytes) / BytesPerGigabyte);
}
