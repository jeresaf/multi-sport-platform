using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements;

/// <summary>
/// The scope's tenant's media storage for <c>STORAGE_GB</c> (S06 D20, Q36): Platform counts the bytes of every variant of
/// the tenant's active assets, and Tenancy registers the counter, because Platform depends on no other module. Counted in
/// whole gigabytes (<see cref="StorageQuota"/>). The uploading module checks the quota before storing
/// (<c>IEntitlements.CheckQuotaAsync</c>).
/// </summary>
public sealed class MediaStorageUsage(IMediaAssets media) : IQuotaUsage
{
    public static readonly string Code = TenancyEntitlements.StorageGb.Code;

    public async Task<int> CountAsync(CancellationToken cancellationToken) =>
        StorageQuota.WholeGigabytes(await media.GetStoredBytesAsync(cancellationToken));
}
