using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Contracts.Media;

/// <summary>
/// The scope's tenant's uploaded files (ADR-0014, S06 D16): other modules keep only an asset ID and ask here for the
/// bytes or a URL. Call after the scope's tenant is set: assets are read and written under RLS. The owning module checks
/// its own permission (and the <c>STORAGE_GB</c> quota) before calling.
/// </summary>
public interface IMediaAssets
{
    /// <summary>
    /// Checks the file by its bytes (type and size for <paramref name="purpose"/>), stores it (a photo as its resized
    /// variants, a PDF as uploaded, a JPEG or PNG document re-encoded without its metadata) and returns the new asset's
    /// ID. Refusals: <c>MEDIA_TYPE_NOT_ALLOWED</c>,
    /// <c>MEDIA_TOO_LARGE</c>, <c>MEDIA_IMAGE_UNREADABLE</c>. The asset is saved at once, in Platform's own unit of work,
    /// without the request's audit entry: the calling module's save records that (so a refused save still audits as a
    /// failure). If the caller's save then fails, the asset stays <c>ACTIVE</c> and unreferenced.
    /// </summary>
    Task<Result<Guid>> StoreAsync(Stream content, MediaPurpose purpose, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the asset <c>REMOVED</c> (<c>ACTIVE → REMOVED</c>, Q39): no URL or read serves it, and it no longer counts
    /// toward storage. Its files stay until a retention job (Later). Saved at once, like <see cref="StoreAsync"/>, so a
    /// caller that replaces an asset removes the old one only after its own save succeeded: a failure then leaves an
    /// unreferenced active asset, never a reference to a removed one. Removing a removed asset is a no-op; an unknown one
    /// is <c>MEDIA_NOT_FOUND</c>.
    /// </summary>
    Task<Result> RemoveAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a variant of an active asset: <see cref="MediaVariant.Original"/> for a document, a size for a photo.
    /// Anything else is <c>MEDIA_NOT_FOUND</c>. The caller disposes the stream.
    /// </summary>
    Task<Result<MediaContent>> OpenAsync(Guid assetId, MediaVariant variant, CancellationToken cancellationToken);

    /// <summary>
    /// A short-lived URL for a variant of an active asset (private assets: S06 D17), relative to the API's origin for the
    /// local storage. Never cache it; ask again when <see cref="SignedMediaUrl.ExpiresAt"/> has passed.
    /// </summary>
    Task<Result<SignedMediaUrl>> GetSignedUrlAsync(Guid assetId, MediaVariant variant, CancellationToken cancellationToken);

    /// <summary>The bytes the tenant stores: every variant of its active assets (<c>STORAGE_GB</c>, S06 D20).</summary>
    Task<long> GetStoredBytesAsync(CancellationToken cancellationToken);
}

/// <summary>What an asset is for; decides the allowed types, the size limit and the variants.</summary>
public enum MediaPurpose
{
    /// <summary>A person's photo: JPEG, PNG or WebP up to 5 MB, stored as WebP thumbnail, medium and large.</summary>
    PersonPhoto,

    /// <summary>
    /// A guardian consent's evidence: PDF, JPEG or PNG up to 10 MB. A PDF is stored as uploaded; an image is re-encoded
    /// at its own size and type without its metadata (a GPS position, the device).
    /// </summary>
    ConsentEvidence,
}

/// <summary>A stored form of an asset.</summary>
public enum MediaVariant
{
    /// <summary>
    /// A document: the PDF as uploaded, or the image re-encoded at its own size (a photo's original isn't kept).
    /// </summary>
    Original,

    /// <summary>A photo at most 160 px on its longest side.</summary>
    Thumbnail,

    /// <summary>A photo at most 640 px on its longest side.</summary>
    Medium,

    /// <summary>A photo at most 1600 px on its longest side.</summary>
    Large,
}

/// <summary>A URL that serves one variant until <paramref name="ExpiresAt"/>.</summary>
public sealed record SignedMediaUrl(string Url, DateTimeOffset ExpiresAt);

/// <summary>An open variant: its bytes, type and length. Dispose it.</summary>
public sealed record MediaContent(Stream Content, string ContentType, long Length) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
