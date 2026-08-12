using System.Collections.Concurrent;
using Minio;
using Minio.DataModel.Args;

namespace SportsPlatform.Api.Services;

public sealed record BrandingAsset(byte[] Content, string ContentType);

public interface IBrandingAssetStorage
{
    Task StoreAsync(Guid tenantId, string fileName, Stream content, long length, string contentType, CancellationToken cancellationToken);
    Task<BrandingAsset?> GetAsync(Guid tenantId, string fileName, CancellationToken cancellationToken);
    Task DeleteAsync(Guid tenantId, string fileName, CancellationToken cancellationToken);
}

public sealed class MinioBrandingAssetStorage : IBrandingAssetStorage
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioBrandingAssetStorage(IConfiguration configuration)
    {
        var endpoint = new Uri(configuration["Storage:Endpoint"]
            ?? throw new InvalidOperationException("Storage:Endpoint is required."));
        _bucket = configuration["Storage:Bucket"]
            ?? throw new InvalidOperationException("Storage:Bucket is required.");
        _client = new MinioClient()
            .WithEndpoint(endpoint.Host, endpoint.Port)
            .WithCredentials(
                configuration["Storage:AccessKey"],
                configuration["Storage:SecretKey"])
            .WithSSL(endpoint.Scheme == Uri.UriSchemeHttps)
            .Build();
    }

    public async Task StoreAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(ObjectName(tenantId, fileName))
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType(contentType), cancellationToken);
    }

    public async Task<BrandingAsset?> GetAsync(
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureBucketAsync(cancellationToken);
            var objectName = ObjectName(tenantId, fileName);
            var stat = await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_bucket).WithObject(objectName), cancellationToken);
            await using var output = new MemoryStream();
            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(output)), cancellationToken);
            return new BrandingAsset(output.ToArray(), stat.ContentType ?? "application/octet-stream");
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucket).WithObject(ObjectName(tenantId, fileName)), cancellationToken);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady) return;
        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady) return;
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucket), cancellationToken);
            if (!exists)
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucket), cancellationToken);
            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private static string ObjectName(Guid tenantId, string fileName) =>
        $"branding/{tenantId:D}/{fileName}";
}

public sealed class InMemoryBrandingAssetStorage : IBrandingAssetStorage
{
    private readonly ConcurrentDictionary<string, BrandingAsset> _assets = new(StringComparer.Ordinal);

    public async Task StoreAsync(Guid tenantId, string fileName, Stream content, long length, string contentType, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream((int)length);
        await content.CopyToAsync(output, cancellationToken);
        _assets[$"{tenantId:D}/{fileName}"] = new BrandingAsset(output.ToArray(), contentType);
    }

    public Task<BrandingAsset?> GetAsync(Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        _assets.TryGetValue($"{tenantId:D}/{fileName}", out var asset);
        return Task.FromResult(asset);
    }

    public Task DeleteAsync(Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        _assets.TryRemove($"{tenantId:D}/{fileName}", out _);
        return Task.CompletedTask;
    }
}
