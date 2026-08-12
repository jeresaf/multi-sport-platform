using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class BrandingLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid NbaTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly ApiWebApplicationFactory _factory;

    public BrandingLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Branding_IsValidatedUploadedAuditedAndExposedToPublicAndMobileClients()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@sportsplatform.com", "Admin123!", "branding-lifecycle");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await PutBrandingAsync(client, new { primaryColor = "red" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PutBrandingAsync(client, new { logoUrl = "data:image/png;base64,AAAA" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PutBrandingAsync(client, new { publicSlug = "changed-slug" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var update = await client.PutAsJsonAsync($"/api/branding/tenant/{PremierTenantId}", new
        {
            tenantName = "Premier Dynamic Brand", shortName = "PDB",
            logoUrl = "https://assets.example.test/premier.png",
            primaryColor = "#0f766e", secondaryColor = "#7C3AED", accentColor = "#F59E0B",
            publicSlug = "premier-league", contactEmail = "HELLO@PREMIER.EXAMPLE",
            contactPhone = "+254700000000", website = "https://premier.example"
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

        var rejectedUpload = await UploadAsync(client, new byte[] { 1, 2, 3, 4 }, "fake.png", "image/png");
        rejectedUpload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        var upload = await UploadAsync(client, png, "tenant-logo.png", "image/png");
        upload.StatusCode.Should().Be(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var logoUrl = uploaded.GetProperty("logoUrl").GetString()!;
        logoUrl.Should().MatchRegex($"/api/branding/assets/{PremierTenantId:D}/[0-9a-f]{{32}}\\.png$");

        var assetPath = new Uri(logoUrl).PathAndQuery;
        var asset = await client.GetAsync(assetPath);
        asset.StatusCode.Should().Be(HttpStatusCode.OK);
        asset.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await asset.Content.ReadAsByteArrayAsync()).Should().Equal(png);
        asset.Headers.CacheControl!.ToString().Should().Contain("immutable");

        var replace = await client.PutAsJsonAsync($"/api/branding/tenant/{PremierTenantId}", new
        {
            tenantName = "Premier Dynamic Brand", shortName = "PDB",
            logoUrl = "https://assets.example.test/replacement.png",
            primaryColor = "#0F766E", secondaryColor = "#7C3AED", accentColor = "#F59E0B",
            publicSlug = "premier-league", contactEmail = "hello@premier.example",
            contactPhone = "+254700000000", website = "https://premier.example/"
        });
        replace.StatusCode.Should().Be(HttpStatusCode.OK, await replace.Content.ReadAsStringAsync());
        (await client.GetAsync(assetPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var publicTenant = await client.GetFromJsonAsync<JsonElement>("/api/public/tenants/premier-league");
        var branding = publicTenant.GetProperty("branding");
        branding.GetProperty("logoUrl").GetString().Should().Be("https://assets.example.test/replacement.png");
        branding.GetProperty("primaryColor").GetString().Should().Be("#0F766E");
        branding.GetProperty("publicSlug").GetString().Should().Be("premier-league");
        branding.GetProperty("contactEmail").GetString().Should().Be("hello@premier.example");
        branding.GetProperty("contactPhone").GetString().Should().Be("+254700000000");
        branding.GetProperty("website").GetString().Should().Be("https://premier.example/");

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.TenantUsageCounters.Where(item => item.TenantId == PremierTenantId
                && item.Metric == "storage_bytes").Select(item => item.Value).SingleAsync())
            .Should().Be(0);
        (await database.AuditEntries.Where(entry => entry.TenantId == PremierTenantId)
            .Select(entry => entry.Action).ToArrayAsync())
            .Should().Contain(new[] { "TenantBrandingUpdated", "TenantLogoUploaded" });
    }

    [Fact]
    public async Task TenantAdministrator_CannotReadOrUploadAnotherTenantsBranding()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@premier.com", "TenantAdmin123!", "branding-isolation");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync($"/api/branding/tenant/{NbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        body.Add(content, "file", "blocked.png");
        (await client.PostAsync($"/api/branding/tenant/{NbaTenantId}/logo", body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> PutBrandingAsync(HttpClient client, object change)
    {
        var baseline = new Dictionary<string, object?>
        {
            ["tenantName"] = "Premier Validation", ["shortName"] = "PV",
            ["logoUrl"] = null, ["primaryColor"] = "#0F766E",
            ["secondaryColor"] = "#7C3AED", ["accentColor"] = "#F59E0B",
            ["publicSlug"] = "premier-league", ["contactEmail"] = null,
            ["contactPhone"] = null, ["website"] = null
        };
        foreach (var property in change.GetType().GetProperties())
            baseline[property.Name] = property.GetValue(change);
        return await client.PutAsJsonAsync($"/api/branding/tenant/{PremierTenantId}", baseline);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        using var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        body.Add(content, "file", fileName);
        return await client.PostAsync($"/api/branding/tenant/{PremierTenantId}/logo", body);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Branding lifecycle test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
}
