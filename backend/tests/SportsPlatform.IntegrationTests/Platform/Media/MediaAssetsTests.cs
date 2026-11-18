using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog.Events;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Registry;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Platform.Infrastructure.Media;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;
using SportsPlatform.UnitTests.Platform.Media;

namespace SportsPlatform.IntegrationTests.Platform.Media;

/// <summary>
/// <c>IMediaAssets</c> on the real database and the local-disk adapter (S06 D16, D17, D20), and the signed-URL token
/// endpoint. Tenants are IDs only: media assets have no foreign key to Tenancy.
/// </summary>
public sealed class MediaAssetsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsa = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _elssc = new("0192a1b2-0000-7000-8000-00000000c002");
    private static readonly Guid _registrar = new("0192a1b2-0000-7000-8000-0000000000b5");

    [Fact]
    public async Task A_photo_is_stored_as_three_WebP_variants_and_counted_in_bytes()
    {
        var id = await StoreAsync(_kmsa, TestImages.Jpeg(2000, 1500), MediaPurpose.PersonPhoto);

        var asset = await AssetAsync(_kmsa, id);
        asset.Purpose.ShouldBe(MediaPurpose.PersonPhoto);
        asset.ContentType.ShouldBe("image/webp");
        asset.Status.ShouldBe(MediaAssetStatus.Active);
        (asset.UploadedByKind, asset.UploadedById, asset.UploadedAt).ShouldBe((ActorKind.User, _registrar, App.Clock.GetUtcNow()));
        asset.Files.Select(f => (f.Variant, f.Width, f.Height)).ShouldBe(
            [(MediaVariant.Thumbnail, 160, 120), (MediaVariant.Medium, 640, 480), (MediaVariant.Large, 1600, 1200)]);
        foreach (var file in asset.Files)
        {
            file.Key.ShouldStartWith($"{_kmsa:N}/{id:N}/");
            new FileInfo(Path.Combine(Fixture.MediaRoot, file.Key)).Length.ShouldBe(file.Bytes);
        }

        asset.SizeBytes.ShouldBe(asset.Files.Sum(f => f.Bytes));
        (await StoredBytesAsync(_kmsa)).ShouldBe(asset.SizeBytes);
        (await StoredBytesAsync(_elssc)).ShouldBe(0);
    }

    [Fact]
    public async Task Evidence_is_stored_as_uploaded()
    {
        var pdf = TestImages.Pdf();

        var id = await StoreAsync(_kmsa, pdf, MediaPurpose.ConsentEvidence);

        var file = (await AssetAsync(_kmsa, id)).Files.ShouldHaveSingleItem();
        (file.Variant, file.ContentType, file.Bytes).ShouldBe((MediaVariant.Original, "application/pdf", pdf.LongLength));
        (await File.ReadAllBytesAsync(Path.Combine(Fixture.MediaRoot, file.Key), Ct)).ShouldBe(pdf);
    }

    [Fact]
    public async Task A_photo_one_byte_over_5_MB_is_too_large_and_nothing_is_stored()
    {
        // A tenant of its own: the media folder outlives the database reset.
        var tenant = Guid.CreateVersion7();
        var jpeg = TestImages.Jpeg(64, 64);
        var oversized = new byte[(5 * 1024 * 1024) + 1];
        jpeg.CopyTo(oversized, 0);

        var result = await TryStoreAsync(tenant, oversized, MediaPurpose.PersonPhoto);

        result.Error!.Code.ShouldBe("MEDIA_TOO_LARGE");
        await ShouldHaveNoAssetsAsync(tenant);
    }

    [Theory]
    [InlineData(MediaPurpose.PersonPhoto, "pdf")]
    [InlineData(MediaPurpose.PersonPhoto, "html")]
    [InlineData(MediaPurpose.ConsentEvidence, "webp")]
    [InlineData(MediaPurpose.ConsentEvidence, "html")]
    public async Task A_file_whose_bytes_are_not_an_allowed_type_is_refused(MediaPurpose purpose, string kind)
    {
        // A tenant of its own: the media folder outlives the database reset.
        var tenant = Guid.CreateVersion7();
        var bytes = kind switch
        {
            "pdf" => TestImages.Pdf(),
            "webp" => TestImages.WebP(32, 32),
            _ => Encoding.UTF8.GetBytes("<html><body><script>alert('photo.jpg')</script></body></html>"),
        };

        var result = await TryStoreAsync(tenant, bytes, purpose);

        result.Error!.Code.ShouldBe("MEDIA_TYPE_NOT_ALLOWED");
        await ShouldHaveNoAssetsAsync(tenant);
    }

    [Fact]
    public async Task A_truncated_image_is_refused()
    {
        // A tenant of its own: the media folder outlives the database reset.
        var tenant = Guid.CreateVersion7();
        var jpeg = TestImages.Jpeg(800, 600);

        var result = await TryStoreAsync(tenant, jpeg[..(jpeg.Length * 2 / 3)], MediaPurpose.PersonPhoto);

        result.Error!.Code.ShouldBe("MEDIA_IMAGE_UNREADABLE");
        await ShouldHaveNoAssetsAsync(tenant);
    }

    [Fact]
    public async Task A_signed_URL_serves_the_file_until_it_expires_and_is_never_cached()
    {
        var id = await StoreAsync(_kmsa, TestImages.Png(800, 400), MediaPurpose.PersonPhoto);
        var url = (await SignedUrlAsync(_kmsa, id, MediaVariant.Medium)).Value;
        url.ExpiresAt.ShouldBe(App.Clock.GetUtcNow() + TimeSpan.FromMinutes(10));
        using var client = App.CreateClient();

        using (var response = await client.GetAsync(url.Url, Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
            response.Headers.CacheControl!.ToString().ShouldBe("no-store, private");
            response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
            TestImages.SizeOf(await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe((640, 320));
        }

        App.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        using (var stillValid = await client.GetAsync(url.Url, Ct))
        {
            stillValid.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        using var expired = await client.GetAsync(url.Url, Ct);
        await expired.ShouldBeProblemAsync(HttpStatusCode.NotFound, "MEDIA_NOT_FOUND");
        expired.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task A_signed_URLs_token_reaches_no_log_event_or_trace()
    {
        var id = await StoreAsync(_kmsa, TestImages.Pdf(), MediaPurpose.ConsentEvidence);
        var url = (await SignedUrlAsync(_kmsa, id, MediaVariant.Original)).Value;
        var token = url.Url.Split('/')[^1];
        var correlationId = $"kmsa-media-{Guid.NewGuid():N}";
        using var client = App.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        using var response = await client.GetAsync(url.Url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completion = await App.Telemetry.WaitForEventAsync(e => e.Properties.ContainsKey("StatusCode")
            && e.Properties.TryGetValue("CorrelationId", out var value) && value.ToString().Contains(correlationId, StringComparison.Ordinal));
        completion.Properties["RequestPath"].ShouldBe(new ScalarValue("/api/public/v1/media/{token}"));
        var span = await App.Telemetry.WaitForSpanAsync(s => s.Kind == ActivityKind.Server && s.TraceId == completion.TraceId);
        span.GetTagItem("url.path").ShouldBe("/api/public/v1/media/{token}");
        App.Telemetry.Events.ShouldAllBe(e => !e.RenderMessage(null).Contains(token)
            && e.Properties.Values.All(value => !value.ToString().Contains(token, StringComparison.Ordinal)));
        App.Telemetry.Spans.ShouldAllBe(s => !s.DisplayName.Contains(token)
            && s.TagObjects.All(tag => !(tag.Value ?? "").ToString()!.Contains(token, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("jpeg", "image/jpeg")]
    public async Task Evidence_is_served_as_a_download_whatever_its_type(string kind, string contentType)
    {
        var bytes = kind == "pdf" ? TestImages.Pdf() : TestImages.WithExif(TestImages.Jpeg(300, 400), orientation: 1);
        var id = await StoreAsync(_kmsa, bytes, MediaPurpose.ConsentEvidence);
        var url = (await SignedUrlAsync(_kmsa, id, MediaVariant.Original)).Value;
        using var client = App.CreateClient();

        using var response = await client.GetAsync(url.Url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName.ShouldBe($"{id:N}.{(kind == "pdf" ? "pdf" : "jpg")}");
        var served = await response.Content.ReadAsByteArrayAsync(Ct);
        if (kind == "pdf")
        {
            served.ShouldBe(bytes);
        }
        else
        {
            // A photographed form is re-encoded: same size and type, no EXIF (GPS) data (review #104 Q1).
            TestImages.SizeOf(served).ShouldBe((300, 400));
            served.AsSpan().IndexOf("Exif"u8).ShouldBe(-1);
        }
    }

    [Fact]
    public async Task A_photo_variant_is_shown_inline()
    {
        var id = await StoreAsync(_kmsa, TestImages.Jpeg(300, 400), MediaPurpose.PersonPhoto);
        var url = (await SignedUrlAsync(_kmsa, id, MediaVariant.Thumbnail)).Value;
        using var client = App.CreateClient();

        using var response = await client.GetAsync(url.Url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition.ShouldBeNull();
    }

    [Fact]
    public async Task Another_tenant_never_gets_a_URL_for_an_asset_or_reads_it_with_a_token_of_its_own()
    {
        var id = await StoreAsync(_kmsa, TestImages.Jpeg(400, 400), MediaPurpose.PersonPhoto);

        (await SignedUrlAsync(_elssc, id, MediaVariant.Thumbnail)).Error!.Code.ShouldBe("MEDIA_NOT_FOUND");

        // A valid token that names ELSSC and KMSA's asset: the endpoint reads under ELSSC, so RLS finds nothing.
        var (token, _) = App.Services.GetRequiredService<MediaUrlTokens>()
            .Issue(_elssc, id, MediaVariant.Thumbnail);
        using var client = App.CreateClient();
        using var response = await client.GetAsync($"/api/public/v1/media/{token}", Ct);
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "MEDIA_NOT_FOUND");
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("CfDJ8")]
    public async Task A_bad_token_is_not_found(string token)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync($"/api/public/v1/media/{token}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "MEDIA_NOT_FOUND");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task A_variant_the_asset_does_not_have_is_not_found()
    {
        var photo = await StoreAsync(_kmsa, TestImages.Jpeg(400, 400), MediaPurpose.PersonPhoto);
        var evidence = await StoreAsync(_kmsa, TestImages.Pdf(), MediaPurpose.ConsentEvidence);

        (await SignedUrlAsync(_kmsa, photo, MediaVariant.Original)).Error!.Code.ShouldBe("MEDIA_NOT_FOUND");
        (await SignedUrlAsync(_kmsa, evidence, MediaVariant.Thumbnail)).Error!.Code.ShouldBe("MEDIA_NOT_FOUND");
    }

    [Fact]
    public async Task A_removed_asset_is_neither_served_nor_counted()
    {
        var id = await StoreAsync(_kmsa, TestImages.Jpeg(400, 400), MediaPurpose.PersonPhoto);
        var url = (await SignedUrlAsync(_kmsa, id, MediaVariant.Large)).Value;

        (await InScopeAsync(_kmsa, media => media.RemoveAsync(id, Ct))).IsSuccess.ShouldBeTrue();
        (await InScopeAsync(_kmsa, media => media.RemoveAsync(id, Ct))).IsSuccess.ShouldBeTrue();

        var asset = await AssetAsync(_kmsa, id);
        (asset.Status, asset.RemovedByKind, asset.RemovedById).ShouldBe((MediaAssetStatus.Removed, ActorKind.User, _registrar));
        (await StoredBytesAsync(_kmsa)).ShouldBe(0);
        (await SignedUrlAsync(_kmsa, id, MediaVariant.Large)).Error!.Code.ShouldBe("MEDIA_NOT_FOUND");
        using var client = App.CreateClient();
        using var response = await client.GetAsync(url.Url, Ct);
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "MEDIA_NOT_FOUND");
        (await InScopeAsync(_elssc, media => media.RemoveAsync(id, Ct))).Error!.Code.ShouldBe("MEDIA_NOT_FOUND");
    }

    [Fact]
    public async Task Storing_inside_an_audited_action_leaves_the_entry_to_the_callers_save()
    {
        await using var scope = Scope(_kmsa);
        var audit = scope.ServiceProvider.GetRequiredService<AuditScope>();
        audit.Begin(
            new ActionDefinition("person.photo.set", "Set a person's photo.", ActionScope.Tenant, AuditRequirement.Required, "S06",
                scopableBy: [AssignmentScope.Tenant]),
            AuditSource.Api);

        var stored = await scope.ServiceProvider.GetRequiredService<IMediaAssets>()
            .StoreAsync(new MemoryStream(TestImages.Jpeg(200, 200)), MediaPurpose.PersonPhoto, Ct);

        stored.IsSuccess.ShouldBeTrue();
        audit.IsPending.ShouldBeTrue();
        (await OutboxCountAsync("platform")).ShouldBe(0);

        // The caller's own save (Registry's, as T5b's photo endpoint) then records the one entry.
        audit.Describe("Person", Guid.CreateVersion7());
        var registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        registry.Add(RegistryTestData.NewPerson(RegistryTestData.JohnSsekandi));
        await registry.SaveChangesAsync(Ct);

        audit.IsPending.ShouldBeFalse();
        (await OutboxCountAsync("registry")).ShouldBe(1);
        (await OutboxCountAsync("platform")).ShouldBe(0);
    }

    [Fact]
    public async Task Storage_usage_counts_active_bytes_in_whole_gigabytes_rounded_down()
    {
        const long gigabyte = 1L << 30;
        await SaveAssetRowAsync(_kmsa, gigabyte - 1);
        (await StorageUsageAsync(_kmsa)).ShouldBe(0);

        var oneByte = await SaveAssetRowAsync(_kmsa, 1);
        (await StorageUsageAsync(_kmsa)).ShouldBe(1);

        await SaveAssetRowAsync(_kmsa, 2 * gigabyte);
        (await StorageUsageAsync(_kmsa)).ShouldBe(3);
        (await StorageUsageAsync(_elssc)).ShouldBe(0);

        await InScopeAsync(_kmsa, media => media.RemoveAsync(oneByte, Ct));
        (await StorageUsageAsync(_kmsa)).ShouldBe(2);
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

    private async Task<Guid> StoreAsync(Guid tenantId, byte[] bytes, MediaPurpose purpose) =>
        (await TryStoreAsync(tenantId, bytes, purpose)).Value;

    private Task<Result<SignedMediaUrl>> SignedUrlAsync(Guid tenantId, Guid assetId, MediaVariant variant) =>
        InScopeAsync(tenantId, media => media.GetSignedUrlAsync(assetId, variant, Ct));

    private Task<long> StoredBytesAsync(Guid tenantId) => InScopeAsync(tenantId, media => media.GetStoredBytesAsync(Ct));

    private async Task<int> StorageUsageAsync(Guid tenantId)
    {
        await using var scope = Scope(tenantId);
        return await scope.ServiceProvider.GetRequiredService<MediaStorageUsage>().CountAsync(Ct);
    }

    private async Task<MediaAsset> AssetAsync(Guid tenantId, Guid id)
    {
        await using var scope = Scope(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await context.Set<MediaAsset>().FindAsync([id], Ct) ?? throw new InvalidOperationException("No such asset.");
    }

    /// <summary>A row of a given size without files, for the counter (gigabytes of files would slow the run).</summary>
    private async Task<Guid> SaveAssetRowAsync(Guid tenantId, long bytes)
    {
        await using var scope = Scope(tenantId);
        var id = MediaAsset.NewAssetId();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        context.Add(MediaAsset.Create(
            id, MediaPurpose.ConsentEvidence, "application/pdf", $"{tenantId:N}/{id:N}",
            [new MediaAssetFile(MediaVariant.Original, $"{tenantId:N}/{id:N}/original.pdf", "application/pdf", null, null, bytes)],
            SystemActor.Instance, App.Clock.GetUtcNow()));
        await context.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<long> OutboxCountAsync(string schema) =>
        (await QueryAsync(DatabaseRoles.Platform, $"SELECT count(*) FROM {schema}.outbox_messages", reader => reader.GetInt64(0))).Single();

    private async Task ShouldHaveNoAssetsAsync(Guid tenantId)
    {
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM platform.media_assets WHERE tenant_id = @tenant", reader => reader.GetInt64(0),
            new NpgsqlParameter("tenant", tenantId))).ShouldBe([0L]);
        var tenantFolder = Path.Combine(Fixture.MediaRoot, tenantId.ToString("N"));
        (Directory.Exists(tenantFolder) ? Directory.GetFiles(tenantFolder, "*", SearchOption.AllDirectories) : []).ShouldBeEmpty();
    }
}
