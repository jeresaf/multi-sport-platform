namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// How <c>STORAGE_GB</c> counts (S06 D20, Q36): whole gigabytes (2³⁰ bytes) of the tenant's stored media, rounded down, so
/// the usage reads "0 of 10 GB" until the first full gigabyte. Tenancy's counter and the uploading modules' quota checks
/// both count this way.
/// </summary>
public static class StorageQuota
{
    public const string Code = "STORAGE_GB";

    public const long BytesPerGigabyte = 1L << 30;

    public static int WholeGigabytes(long bytes) => (int)Math.Min(int.MaxValue, Math.Max(0, bytes) / BytesPerGigabyte);
}
