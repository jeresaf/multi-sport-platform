using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>
/// Where media files live (ADR-0014): provider-neutral, so only the adapter knows the provider (AR-33). S06 builds the
/// local-disk adapter (<see cref="LocalDiskFileStorage"/>); the Azure Blob adapter comes with the first deployment (Q41).
/// Keys are relative paths such as <c>{tenantId}/{assetId}/thumbnail.webp</c>, written once and never changed.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes <paramref name="content"/> at <paramref name="key"/>, replacing nothing (a key is written once).</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The file at <paramref name="key"/>, or null if there is none. The caller disposes the stream.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes the file at <paramref name="key"/> if it exists.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// A URL that serves <paramref name="file"/> for <see cref="MediaUrlTokens.Lifetime"/> without a sign-in (S06 D17):
    /// the local adapter's token endpoint, a SAS URL for Azure. Only for a file whose asset the caller may read.
    /// </summary>
    SignedMediaUrl GetSignedReadUrl(StoredMediaFile file);
}

/// <summary>One stored file of an asset, as a signed URL names it.</summary>
public sealed record StoredMediaFile(Guid TenantId, Guid AssetId, MediaVariant Variant, string Key, string ContentType);
