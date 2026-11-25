using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.UnitTests.Platform.Media;

namespace SportsPlatform.IntegrationTests.Platform.Media;

/// <summary>
/// Logos and badges (S07 D15, T4a): stored <c>PUBLIC</c> as two WebP variants within their limits, and the public asset
/// route, which serves only an active public asset of the host's tenant and is cacheable for a day.
/// </summary>
public sealed class PublicAssetsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _kmsaHost = "kmsa.localhost";

    private static readonly Guid _registrar = new("0192a1b2-0000-7000-8000-0000000000b5");

    [Fact]
    public async Task A_logo_is_stored_public_as_a_thumbnail_and_a_medium()
    {
        var kmsa = await TenantAsync("kmsa");

        var id = await StoreAsync(kmsa, TestImages.Png(1000, 500), MediaPurpose.TeamBadge);

        var asset = await AssetAsync(kmsa, id);
        (asset.Purpose, asset.Visibility, asset.ContentType).ShouldBe((MediaPurpose.TeamBadge, MediaVisibility.Public, "image/webp"));
        asset.Files.Select(f => (f.Variant, f.Width, f.Height)).ShouldBe(
            [(MediaVariant.Thumbnail, 160, 80), (MediaVariant.Medium, 640, 320)]);
    }

    // T4 acceptance 1: an SVG, a file over 2 MB or an image under 64 px is refused, and nothing is stored.
    [Theory]
    [InlineData("svg", "MEDIA_TYPE_NOT_ALLOWED")]
    [InlineData("over-2-mb", "MEDIA_TOO_LARGE")]
    [InlineData("63-px", "MEDIA_IMAGE_TOO_SMALL")]
    public async Task A_logo_outside_its_limits_is_refused(string kind, string code)
    {
        // A tenant of its own: the media folder outlives the database reset.
        var tenant = Guid.CreateVersion7();
        var bytes = kind switch
        {
            "svg" => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"200\"><script>alert(1)</script></svg>"),
            "over-2-mb" => Oversized(TestImages.Png(200, 200), (2 * 1024 * 1024) + 1),
            _ => TestImages.Png(63, 300),
        };

        var result = await TryStoreAsync(tenant, bytes, MediaPurpose.OrgLogo);

        result.Error!.Code.ShouldBe(code);
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM platform.media_assets WHERE tenant_id = @tenant",
            reader => reader.GetInt64(0), new NpgsqlParameter("tenant", tenant))).ShouldBe([0L]);
        Directory.Exists(Path.Combine(Fixture.MediaRoot, tenant.ToString("N"))).ShouldBeFalse();
    }

    [Fact]
    public async Task A_logo_of_exactly_2_MB_is_accepted()
    {
        var kmsa = await TenantAsync("kmsa");

        var result = await TryStoreAsync(kmsa, Oversized(TestImages.Png(200, 200), 2 * 1024 * 1024), MediaPurpose.OrgLogo);

        result.IsSuccess.ShouldBeTrue();
    }

    // T4 acceptance 2: the route serves the host's tenant's public asset, cacheable for a day.
    [Theory]
    [InlineData(MediaVariant.Thumbnail, 160, 80)]
    [InlineData(MediaVariant.Medium, 640, 320)]
    public async Task The_public_route_serves_a_logo_on_its_tenants_host_cacheable_for_a_day(MediaVariant variant, int width, int height)
    {
        var kmsa = await TenantAsync("kmsa");
        var id = await StoreAsync(kmsa, TestImages.Png(1000, 500), MediaPurpose.OrgLogo);
        var path = await InScopeAsync(kmsa, media => Task.FromResult(media.GetPublicPath(id, variant)));
        path.ShouldBe($"/api/public/v1/assets/{id}/{variant.ToString().ToLowerInvariant()}");

        var (status, contentType, headers, body) = await PublicBytesAsync(path, _kmsaHost);

        status.ShouldBe(StatusCodes.Status200OK);
        contentType.ShouldBe("image/webp");
        headers.CacheControl?.ToString().ShouldBe("public, max-age=86400");
        headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        TestImages.SizeOf(body).ShouldBe((width, height));
        // Review #132 Q1: the server sends the stored file's length (read as the server wrote it, not the client's count).
        (await App.Server.GetAsync(path, _kmsaHost)).Headers.ContentLength.ShouldBe(body.LongLength);
    }

    // T4 acceptance 2: another tenant's asset, a private one (a person's photo) and a removed one are 404.
    [Fact]
    public async Task The_public_route_never_serves_another_tenants_a_private_or_a_removed_asset()
    {
        var kmsa = await TenantAsync("kmsa");
        var elssc = await TenantAsync("elssc");
        var elsscLogo = await StoreAsync(elssc, TestImages.Png(300, 300), MediaPurpose.OrgLogo);
        var photo = await StoreAsync(kmsa, TestImages.Jpeg(300, 300), MediaPurpose.PersonPhoto);
        var removed = await StoreAsync(kmsa, TestImages.Png(300, 300), MediaPurpose.TeamLogo);
        (await InScopeAsync(kmsa, media => media.RemoveAsync(removed, Ct))).IsSuccess.ShouldBeTrue();

        foreach (var path in new[]
        {
            $"/api/public/v1/assets/{elsscLogo}/medium",
            $"/api/public/v1/assets/{photo}/medium",
            $"/api/public/v1/assets/{photo}/thumbnail",
            $"/api/public/v1/assets/{removed}/thumbnail",
            $"/api/public/v1/assets/{Guid.CreateVersion7()}/thumbnail",
        })
        {
            var response = await App.Server.GetAsync(path, _kmsaHost);
            response.ShouldBeProblem(StatusCodes.Status404NotFound, "MEDIA_NOT_FOUND");
            response.Headers.CacheControl.ToString().ShouldBe("no-store");
        }

        // The other tenant's own host serves it.
        (await PublicBytesAsync($"/api/public/v1/assets/{elsscLogo}/medium", "elssc.localhost")).Status.ShouldBe(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(MediaVariant.Large)]
    [InlineData(MediaVariant.Original)]
    public async Task Only_a_thumbnail_or_a_medium_has_a_public_path(MediaVariant variant)
    {
        var kmsa = await TenantAsync("kmsa");

        await using var scope = Scope(kmsa);
        var media = scope.ServiceProvider.GetRequiredService<IMediaAssets>();

        Should.Throw<ArgumentOutOfRangeException>(() => media.GetPublicPath(Guid.CreateVersion7(), variant));
    }

    [Theory]
    [InlineData("large")]
    [InlineData("original")]
    [InlineData("MEDIUM")]
    public async Task A_variant_the_route_does_not_serve_is_not_found(string variant)
    {
        var kmsa = await TenantAsync("kmsa");
        var id = await StoreAsync(kmsa, TestImages.Png(300, 300), MediaPurpose.OrgLogo);

        (await App.Server.GetAsync($"/api/public/v1/assets/{id}/{variant}", _kmsaHost))
            .ShouldBeProblem(StatusCodes.Status404NotFound, "MEDIA_NOT_FOUND");
    }

    [Fact]
    public async Task A_site_that_is_not_public_serves_no_asset()
    {
        var hidden = await TestTenantAsync("hidden", publicEnabled: false);
        var id = await StoreAsync(hidden, TestImages.Png(300, 300), MediaPurpose.OrgLogo);

        var response = await App.Server.GetAsync($"/api/public/v1/assets/{id}/medium", "hidden.localhost");

        // As every public route of a site that isn't public (S06-T6b).
        response.ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");
    }

    [Fact]
    public async Task The_database_refuses_a_public_person_photo()
    {
        var kmsa = await TenantAsync("kmsa");
        await using var scope = Scope(kmsa);
        var id = MediaAsset.NewAssetId();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        context.Add(MediaAsset.Create(
            id, MediaPurpose.PersonPhoto, MediaVisibility.Public, "image/webp", $"{kmsa:N}/{id:N}",
            [new MediaAssetFile(MediaVariant.Thumbnail, $"{kmsa:N}/{id:N}/thumbnail.webp", "image/webp", 160, 160, 1_000)],
            SystemActor.Instance, App.Clock.GetUtcNow()));

        var refused = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        refused.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("ck_media_assets_public_purpose");
    }

    private Task<Guid> TenantAsync(string slug) => TestTenantAsync(slug, publicEnabled: true);

    private Task<Guid> TestTenantAsync(string slug, bool publicEnabled) =>
        TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, publicEnabled, withDomain: true, Ct);

    /// <summary>A valid image padded to <paramref name="length"/> bytes (the type is judged by the first bytes).</summary>
    private static byte[] Oversized(byte[] image, int length)
    {
        var padded = new byte[length];
        image.CopyTo(padded, 0);
        return padded;
    }

    private AsyncServiceScope Scope(Guid tenantId)
    {
        var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(new UserActor(_registrar));
        return scope;
    }

    private async Task<T> InScopeAsync<T>(Guid tenantId, Func<IMediaAssets, Task<T>> action)
    {
        await using var scope = Scope(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<IMediaAssets>());
    }

    private Task<Result<Guid>> TryStoreAsync(Guid tenantId, byte[] bytes, MediaPurpose purpose) =>
        InScopeAsync(tenantId, media => media.StoreAsync(new MemoryStream(bytes), purpose, Ct));

    private async Task<Guid> StoreAsync(Guid tenantId, byte[] bytes, MediaPurpose purpose)
    {
        var stored = await TryStoreAsync(tenantId, bytes, purpose);
        stored.IsSuccess.ShouldBeTrue(stored.Error?.Code);
        return stored.Value;
    }

    private async Task<MediaAsset> AssetAsync(Guid tenantId, Guid id)
    {
        await using var scope = Scope(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await context.Set<MediaAsset>().FindAsync([id], Ct) ?? throw new InvalidOperationException("No such asset.");
    }

    /// <summary>A public GET through an <see cref="HttpClient"/> on a tenant's host, kept as bytes (an image).</summary>
    private async Task<(int Status, string? ContentType, System.Net.Http.Headers.HttpResponseHeaders Headers, byte[] Body)> PublicBytesAsync(
        string path, string host)
    {
        using var client = App.Server.CreateClient();
        client.BaseAddress = new Uri($"https://{host}");
        var response = await client.GetAsync(path, Ct);
        return ((int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, response.Headers,
            await response.Content.ReadAsByteArrayAsync(Ct));
    }
}
