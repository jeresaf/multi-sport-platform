using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Tenancy.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class TenantLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly ApiWebApplicationFactory _factory;

    public TenantLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PlatformOwner_ManagesCompleteTenantLifecycleContactsAndLocalization()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@sportsplatform.com", "Admin123!", "tenant-lifecycle-platform");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var suffix = Guid.NewGuid().ToString("N");
        var slug = $"acceptance-{suffix}";

        var invalid = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = "Invalid slug", shortName = "INV", slug = "Invalid Slug"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var create = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = "Tenant acceptance", shortName = "TA", slug
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = created.GetProperty("id").GetGuid();

        var duplicate = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = "Duplicate", shortName = "DUP", slug
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var localization = await client.PutAsJsonAsync($"/api/tenants/{tenantId}/localization", new
        {
            country = "Kenya", timezone = "Africa/Nairobi", currency = "kes", defaultLanguage = "en-KE"
        });
        localization.StatusCode.Should().Be(HttpStatusCode.OK, await localization.Content.ReadAsStringAsync());

        var firstContact = await CreateContactAsync(client, tenantId, "Operations", "ops@example.test", true);
        var secondContact = await CreateContactAsync(client, tenantId, "Director", "director@example.test", true);
        var contacts = await client.GetFromJsonAsync<JsonElement[]>($"/api/tenants/{tenantId}/contacts");
        contacts.Should().HaveCount(2);
        var contactItems = contacts!;
        contactItems.Single(item => item.GetProperty("id").GetGuid() == secondContact)
            .GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        contactItems.Single(item => item.GetProperty("id").GetGuid() == firstContact)
            .GetProperty("isPrimary").GetBoolean().Should().BeFalse();

        (await client.PostAsync($"/api/tenants/{tenantId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/tenants/{tenantId}/activate", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/tenants/{tenantId}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var reactivate = await client.PostAsync($"/api/tenants/{tenantId}/activate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.Roles.Where(role => role.TenantId == tenantId).Select(role => role.Name).ToArrayAsync())
            .Should().Contain(SystemRoleCatalogue.TenantRoles.Select(role => role.Name));
        var tenant = await database.Tenants.SingleAsync(item => item.Id == tenantId);
        tenant.Currency.Should().Be("KES");
        tenant.Status.Should().Be(TenantStatus.Archived);
        (await database.AuditEntries.Where(entry => entry.TenantId == tenantId).Select(entry => entry.Action).ToArrayAsync())
            .Should().Contain(new[] { "TenantCreated", "TenantLocalizationUpdated", "TenantContactCreated", "TenantSuspended", "TenantActivated", "TenantArchived" });
    }

    [Fact]
    public async Task SuspendedTenant_CanReadOwnTenantButCannotPerformProtectedWrites()
    {
        using var platform = _factory.CreateClient();
        var platformToken = await LoginAsync(platform, "admin@sportsplatform.com", "Admin123!", "tenant-suspension-platform");
        platform.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
        (await platform.PostAsync($"/api/tenants/{PremierTenantId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            using var tenantClient = _factory.CreateClient();
            var tenantToken = await LoginAsync(tenantClient, "admin@premier.com", "TenantAdmin123!", "tenant-suspension-owner");
            tenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
            (await tenantClient.GetAsync($"/api/tenants/{PremierTenantId}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var write = await tenantClient.PutAsJsonAsync($"/api/tenants/{PremierTenantId}/localization", new
            {
                country = "Blocked", timezone = "UTC", currency = "USD", defaultLanguage = "en"
            });
            write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await write.Content.ReadAsStringAsync()).Should().Contain("TenantSuspended");

            var visible = await tenantClient.GetFromJsonAsync<JsonElement[]>("/api/tenants");
            visible.Should().ContainSingle();
            visible![0].GetProperty("id").GetGuid().Should().Be(PremierTenantId);
        }
        finally
        {
            (await platform.PostAsync($"/api/tenants/{PremierTenantId}/activate", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private static async Task<Guid> CreateContactAsync(HttpClient client, Guid tenantId, string name, string email, bool primary)
    {
        var response = await client.PostAsJsonAsync($"/api/tenants/{tenantId}/contacts", new
        {
            name, email, phone = "+254700000000", role = "Administration", isPrimary = primary
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Tenant lifecycle test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
}
