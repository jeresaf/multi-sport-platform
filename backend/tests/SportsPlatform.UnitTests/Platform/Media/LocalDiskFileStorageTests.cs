using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class LocalDiskFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"msp-media-unit-{Guid.NewGuid():N}");
    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests() => _storage = Storage(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Writes_reads_and_deletes_a_file_under_the_root()
    {
        var ct = TestContext.Current.CancellationToken;
        await _storage.PutAsync("t1/a1/original.pdf", new MemoryStream(TestImages.Pdf()), "application/pdf", ct);

        File.Exists(Path.Combine(_root, "t1", "a1", "original.pdf")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(_root, "t1", "a1")).Length.ShouldBe(1);
        await using (var stream = await _storage.OpenAsync("t1/a1/original.pdf", ct))
        {
            using var copy = new MemoryStream();
            await stream!.CopyToAsync(copy, ct);
            copy.ToArray().ShouldBe(TestImages.Pdf());
        }

        await _storage.DeleteAsync("t1/a1/original.pdf", ct);
        (await _storage.OpenAsync("t1/a1/original.pdf", ct)).ShouldBeNull();
    }

    [Fact]
    public async Task A_key_is_written_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await _storage.PutAsync("t1/a1/large.webp", new MemoryStream([1]), "image/webp", ct);

        await Should.ThrowAsync<IOException>(() => _storage.PutAsync("t1/a1/large.webp", new MemoryStream([2]), "image/webp", ct));
        Directory.GetFiles(Path.Combine(_root, "t1", "a1")).Length.ShouldBe(1);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("t1/../../outside.txt")]
    [InlineData("t1/./a1.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("t1\\a1.txt")]
    [InlineData("t1//a1.txt")]
    public void A_key_never_leaves_the_root(string key) =>
        Should.Throw<ArgumentException>(() => _storage.PathOf(key));

    [Fact]
    public void Without_a_root_nothing_is_stored() =>
        Should.Throw<InvalidOperationException>(() => Storage(root: null).PathOf("t1/a1.txt"));

    [Fact]
    public void A_signed_URL_is_the_token_endpoint()
    {
        var url = _storage.GetSignedReadUrl(new StoredMediaFile(Guid.NewGuid(), Guid.NewGuid(), MediaVariant.Thumbnail, "t/a/thumbnail.webp", "image/webp"));

        url.Url.ShouldStartWith("/api/public/v1/media/");
        url.ExpiresAt.ShouldBe(new DateTimeOffset(2026, 11, 18, 9, 10, 0, TimeSpan.Zero));
    }

    private static LocalDiskFileStorage Storage(string? root) => new(
        Options.Create(new MediaOptions { LocalRoot = root }),
        new MediaUrlTokens(new EphemeralDataProtectionProvider(), new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero))));
}
