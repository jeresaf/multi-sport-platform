using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>
/// <see cref="IFileStorage"/> on the local disk (Development, tests; S06 D16). A file is written to a temporary name and
/// moved into place, so a reader never sees half a file. Keys stay inside <see cref="MediaOptions.LocalRoot"/>: a rooted
/// key, a <c>..</c> segment or a backslash is a bug and throws. Signed URLs point at the API's token endpoint.
/// </summary>
public sealed class LocalDiskFileStorage(IOptions<MediaOptions> options, MediaUrlTokens tokens) : IFileStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    public SignedMediaUrl GetSignedReadUrl(StoredMediaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var (token, expiresAt) = tokens.Issue(file.TenantId, file.AssetId, file.Variant);
        return new SignedMediaUrl($"/api/public/v1/media/{token}", expiresAt);
    }

    /// <summary>The full path of <paramref name="key"/> under the root; throws if the key would leave it.</summary>
    public string PathOf(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Path.IsPathRooted(key) || key.Contains('\\', StringComparison.Ordinal)
            || key.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A storage key is a relative path without '.' or '..' segments.", nameof(key));
        }

        var root = Root();
        var path = Path.GetFullPath(Path.Combine(root, key));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("A storage key must stay inside the media root.", nameof(key));
    }

    private string Root()
    {
        var configured = options.Value.LocalRoot;
        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException($"{MediaOptions.SectionName}:{nameof(MediaOptions.LocalRoot)} is not set; media can't be stored or read.")
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
    }
}
