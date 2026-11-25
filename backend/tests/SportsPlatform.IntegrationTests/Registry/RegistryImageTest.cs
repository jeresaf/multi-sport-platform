using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>What the organisation and team image tests share (S07 D15): the form, the answer, the assets and the quota.</summary>
public abstract class RegistryImageTest(PostgresFixture fixture) : DatabaseTest(fixture)
{
    protected static MultipartFormDataContent Form(byte[] bytes, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new() { { file, "file", "logo.png" } };
    }

    /// <summary>The 200 answer, with an <c>ETag</c> of its version.</summary>
    protected static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{body.GetProperty("version").GetUInt32()}\"");
            return body;
        }
    }

    /// <summary>
    /// The image's two signed URLs serve WebP at 160 and 640 px on the longest side (never enlarged; no <c>large</c>), and
    /// expire with the URLs' life.
    /// </summary>
    protected async Task ShouldServeImageAsync(HttpClient client, JsonElement image, int width, int height)
    {
        image.TryGetProperty("largeUrl", out _).ShouldBeFalse();
        image.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow() + TimeSpan.FromMinutes(10));
        foreach (var (property, longest) in new[] { ("thumbnailUrl", 160), ("mediumUrl", 640) })
        {
            using var served = await client.GetAsync(image.GetProperty(property).GetString(), Ct);
            served.StatusCode.ShouldBe(HttpStatusCode.OK, property);
            served.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
            var scale = Math.Min(1, (double)longest / Math.Max(width, height));
            TestImages.SizeOf(await served.Content.ReadAsByteArrayAsync(Ct))
                .ShouldBe(((int)Math.Round(width * scale), (int)Math.Round(height * scale)), property);
        }
    }

    protected async Task<(MediaAssetStatus Status, string Purpose, string Visibility)> AssetAsync(Guid tenantId, Guid assetId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var asset = await context.Set<MediaAsset>().SingleAsync(a => a.Id == assetId, Ct);
        var purpose = (await QueryAsync(DatabaseRoles.Platform, "SELECT purpose, visibility FROM platform.media_assets WHERE id = @id",
            reader => (reader.GetString(0), reader.GetString(1)), new NpgsqlParameter("id", assetId))).Single();
        return (asset.Status, purpose.Item1, purpose.Item2);
    }

    protected async Task<long> AssetCountAsync(Guid tenantId) =>
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM platform.media_assets WHERE tenant_id = @tenant",
            reader => reader.GetInt64(0), new NpgsqlParameter("tenant", tenantId))).Single();

    /// <summary>An override setting <c>STORAGE_GB</c> to <paramref name="limit"/> (as the platform records one).</summary>
    protected async Task OverrideStorageAsync(Guid tenantId, int limit)
    {
        await ExecuteAsync(
            DatabaseRoles.App,
            $"SELECT set_config('app.tenant_id', '{tenantId}', false); " +
            "INSERT INTO tenancy.tenant_entitlement_overrides (id, tenant_id, entitlement_code, value, reason, " +
            "created_at, created_by, updated_at, updated_by) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{tenantId}', 'STORAGE_GB', '{{\"limit\": {limit}}}', 'Storage paused for the pilot', " +
            $"now(), '{TenantTestData.StaffId}', now(), '{TenantTestData.StaffId}')");
        App.Services.GetRequiredService<TenantAccessCache>().Clear(tenantId);
    }

    /// <summary>
    /// An active tenant with the test sport enabled, and a client of a member with <paramref name="role"/> tenant-wide.
    /// </summary>
    protected async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        await TenantTestData.EnableSportAsync(App.Services, tenant, TestballFull, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }

    /// <summary>A tenant whose only subscription period was cancelled, so it is read-only, and its registrar's client.</summary>
    protected async Task<(Guid Tenant, HttpClient Client)> ReadOnlyTenantAsync()
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, tenant, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, tenant, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}
