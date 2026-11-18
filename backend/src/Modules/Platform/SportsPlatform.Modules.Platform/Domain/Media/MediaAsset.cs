using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Platform.Domain.Media;

/// <summary>Whether an asset can be served (<c>ACTIVE → REMOVED</c>, Q39; docs/domain/state-machines.md).</summary>
public enum MediaAssetStatus
{
    Active,
    Removed,
}

/// <summary>Who may get the asset: <c>PRIVATE</c> through signed URLs only (every asset in S06); <c>PUBLIC</c> later.</summary>
public enum MediaVisibility
{
    Private,
    Public,
}

/// <summary>One stored file of an asset: its variant, storage key, type, pixel size (images) and bytes.</summary>
public sealed record MediaAssetFile(MediaVariant Variant, string Key, string ContentType, int? Width, int? Height, long Bytes);

/// <summary>
/// An uploaded file of a tenant (<c>platform.media_assets</c>, tenant-owned, ADR-0014, S06 D16): what it is for, its
/// stored files (a photo's variants or a document) and their total size, who uploaded it and when, and its
/// status. Files are written before the row is saved; nothing changes them afterwards. Other modules hold its ID.
/// </summary>
public sealed class MediaAsset : AggregateRoot, ITenantOwned
{
    private MediaAsset()
    {
    }

    private MediaAsset(
        Guid id, MediaPurpose purpose, string contentType, string storageKey, IReadOnlyList<MediaAssetFile> files,
        ICurrentActor actor, DateTimeOffset uploadedAt)
        : base(id)
    {
        Purpose = purpose;
        ContentType = contentType;
        StorageKey = storageKey;
        Files = files;
        SizeBytes = files.Sum(file => file.Bytes);
        Visibility = MediaVisibility.Private;
        Status = MediaAssetStatus.Active;
        UploadedByKind = actor.Kind;
        UploadedById = actor.ActorId;
        UploadedAt = uploadedAt;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public MediaPurpose Purpose { get; private set; }

    /// <summary>The type of the stored content: the document's own, or the variants' (<c>image/webp</c>).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>The bytes of every stored file.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>The folder of the asset's files in <c>IFileStorage</c>: <c>{tenantId}/{assetId}</c>.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public IReadOnlyList<MediaAssetFile> Files { get; private set; } = [];

    public MediaVisibility Visibility { get; private set; }

    public MediaAssetStatus Status { get; private set; }

    public ActorKind UploadedByKind { get; private set; }

    public Guid UploadedById { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public ActorKind? RemovedByKind { get; private set; }

    public Guid? RemovedById { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary>
    /// A new active asset with the files already stored under <paramref name="storageKey"/>. The ID is chosen first
    /// (<see cref="NewAssetId"/>), because the storage key names it.
    /// </summary>
    public static MediaAsset Create(
        Guid id, MediaPurpose purpose, string contentType, string storageKey, IReadOnlyList<MediaAssetFile> files,
        ICurrentActor actor, DateTimeOffset uploadedAt)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An asset needs an ID.", nameof(id));
        }

        if (files.Count == 0 || files.Select(file => file.Variant).Distinct().Count() != files.Count)
        {
            throw new ArgumentException("An asset has at least one file and one file per variant.", nameof(files));
        }

        return new MediaAsset(id, purpose, contentType, storageKey, [.. files], actor, uploadedAt);
    }

    /// <summary>A new asset's ID (UUIDv7), taken before its files are stored.</summary>
    public static Guid NewAssetId() => NewId();

    /// <summary>The stored file of <paramref name="variant"/>, if the asset has one.</summary>
    public MediaAssetFile? FileOf(MediaVariant variant) => Files.FirstOrDefault(file => file.Variant == variant);

    /// <summary><c>ACTIVE → REMOVED</c>; removing a removed asset changes nothing.</summary>
    public void Remove(ICurrentActor actor, DateTimeOffset removedAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Status == MediaAssetStatus.Removed)
        {
            return;
        }

        Status = MediaAssetStatus.Removed;
        RemovedByKind = actor.Kind;
        RemovedById = actor.ActorId;
        RemovedAt = removedAt;
    }
}
