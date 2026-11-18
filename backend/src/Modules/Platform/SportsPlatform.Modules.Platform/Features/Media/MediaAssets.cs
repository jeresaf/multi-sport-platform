using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.Modules.Platform.Features.Media;

/// <summary>
/// <see cref="IMediaAssets"/> (S06 D16, D17, D20): checks a file by its bytes, writes its files through
/// <see cref="IFileStorage"/> and then saves the asset row under the scope's tenant (RLS). If the row can't be saved, the
/// files just written are deleted again.
/// </summary>
public sealed class MediaAssets(
    PlatformDbContext context, IFileStorage storage, ITenantContext tenant, ICurrentActor actor, TimeProvider timeProvider)
    : IMediaAssets
{
    public async Task<Result<Guid>> StoreAsync(Stream content, MediaPurpose purpose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Media is stored under the scope's tenant.");

        var read = await MediaRules.ReadBoundedAsync(content, purpose, cancellationToken);
        if (read.IsFailure)
        {
            return read.Error;
        }

        var bytes = read.Value;
        var type = MediaRules.CheckType(bytes, purpose);
        if (type.IsFailure)
        {
            return type.Error;
        }

        var id = MediaAsset.NewAssetId();
        var storageKey = $"{tenantId:N}/{id:N}";
        var prepared = Prepare(bytes, type.Value, purpose, storageKey);
        if (prepared.IsFailure)
        {
            return prepared.Error;
        }

        var (contentType, files) = prepared.Value;
        var written = new List<string>();
        try
        {
            foreach (var (file, fileBytes) in files)
            {
                using var stream = new MemoryStream(fileBytes, writable: false);
                await storage.PutAsync(file.Key, stream, file.ContentType, cancellationToken);
                written.Add(file.Key);
            }

            var asset = MediaAsset.Create(
                id, purpose, contentType, storageKey, [.. files.Select(f => f.File)], actor, timeProvider.GetUtcNow());
            context.Add(asset);
            var saved = await context.SaveChangesWithoutScopeAuditAsync(cancellationToken);
            if (saved.IsFailure)
            {
                await DeleteQuietlyAsync(written);
                return saved.Error;
            }

            return Result.Success(id);
        }
        catch
        {
            await DeleteQuietlyAsync(written);
            throw;
        }
    }

    public async Task<Result> RemoveAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await context.Set<MediaAsset>().SingleOrDefaultAsync(a => a.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return Result.Failure(PlatformErrors.MediaNotFound);
        }

        if (asset.Status == MediaAssetStatus.Removed)
        {
            return Result.Success();
        }

        asset.Remove(actor, timeProvider.GetUtcNow());
        return await context.SaveChangesWithoutScopeAuditAsync(cancellationToken);
    }

    public async Task<Result<MediaContent>> OpenAsync(Guid assetId, MediaVariant variant, CancellationToken cancellationToken)
    {
        var file = await ActiveFileAsync(assetId, variant, cancellationToken);
        if (file is null)
        {
            return PlatformErrors.MediaNotFound;
        }

        var stream = await storage.OpenAsync(file.Key, cancellationToken)
            ?? throw new InvalidOperationException($"Media asset {assetId} has no stored {variant} file.");
        return Result.Success(new MediaContent(stream, file.ContentType, file.Bytes));
    }

    public async Task<Result<SignedMediaUrl>> GetSignedUrlAsync(Guid assetId, MediaVariant variant, CancellationToken cancellationToken)
    {
        var file = await ActiveFileAsync(assetId, variant, cancellationToken);
        if (file is null)
        {
            return PlatformErrors.MediaNotFound;
        }

        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Media is read under the scope's tenant.");
        return Result.Success(storage.GetSignedReadUrl(new StoredMediaFile(tenantId, assetId, variant, file.Key, file.ContentType)));
    }

    public async Task<long> GetStoredBytesAsync(CancellationToken cancellationToken) =>
        await context.Set<MediaAsset>()
            .Where(a => a.Status == MediaAssetStatus.Active)
            .SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0;

    /// <summary>The stored file of an active asset of the scope's tenant (RLS), if it has that variant.</summary>
    private async Task<MediaAssetFile?> ActiveFileAsync(Guid assetId, MediaVariant variant, CancellationToken cancellationToken)
    {
        var asset = await context.Set<MediaAsset>().AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assetId && a.Status == MediaAssetStatus.Active, cancellationToken);
        return asset?.FileOf(variant);
    }

    /// <summary>The asset's content type and files: a photo's WebP variants, or the document as uploaded.</summary>
    private static Result<(string ContentType, IReadOnlyList<(MediaAssetFile File, byte[] Bytes)> Files)> Prepare(
        byte[] bytes, MediaFileType type, MediaPurpose purpose, string storageKey)
    {
        if (!MediaRules.MakesVariants(purpose))
        {
            var contentType = MediaRules.ContentType(type);
            var key = $"{storageKey}/original.{MediaRules.Extension(type)}";
            return Result.Success<(string, IReadOnlyList<(MediaAssetFile, byte[])>)>(
                (contentType, [(new MediaAssetFile(MediaVariant.Original, key, contentType, null, null, bytes.LongLength), bytes)]));
        }

        var variants = ImageVariants.Create(bytes);
        if (variants.IsFailure)
        {
            return variants.Error;
        }

        var webp = MediaRules.ContentType(MediaFileType.WebP);
        IReadOnlyList<(MediaAssetFile, byte[])> files = [.. variants.Value.Select(image => (
            new MediaAssetFile(
                image.Variant, $"{storageKey}/{image.Variant.ToString().ToLowerInvariant()}.webp", webp, image.Width, image.Height, image.Bytes.LongLength),
            image.Bytes))];
        return Result.Success((webp, files));
    }

    private async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best effort: an unreferenced file costs disk space only, never a wrong read.
            }
        }
    }
}
