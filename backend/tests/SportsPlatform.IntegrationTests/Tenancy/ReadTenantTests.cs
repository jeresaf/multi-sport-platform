using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary><c>GET /api/platform/v1/tenants</c> and <c>GET …/{tenantId}</c> (<c>tenant.read</c>).</summary>
public sealed class ReadTenantTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task Detail_returns_the_tenant_with_its_ETag_and_is_audited_under_it()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Suspended, Ct);
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenant = (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
        tenant.ShouldSatisfyAllConditions(
            t => t.Id.ShouldBe(tenantId),
            t => t.Slug.ShouldBe("elssc"),
            t => t.Status.ShouldBe(TenantStatus.Suspended));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{tenant.Version}\"");
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(tenantId),
            e => e.EntityId.ShouldBe(tenantId));
    }

    [Fact]
    public async Task Unknown_tenant_is_not_found()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri($"{TenantTestData.TenantsPath}/0192a1b2-0000-7000-8000-00000000c404", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task List_pages_through_every_tenant_by_slug()
    {
        foreach (var (slug, status) in new[]
        {
            ("acacia-schools", TenantStatus.Archived),
            ("kmsa", TenantStatus.Active),
            ("elssc", TenantStatus.Provisioning),
            ("jacaranda-games", TenantStatus.Suspended),
            ("sunbird-juniors", TenantStatus.Active),
        })
        {
            await TenantTestData.SaveTenantAsync(App.Services, slug, status, Ct);
        }

        var first = await ListAsync("?limit=2");
        first.Items.Select(t => t.Slug).ShouldBe(["acacia-schools", "elssc"]);
        first.Page.HasMore.ShouldBeTrue();

        var second = await ListAsync($"?limit=2&cursor={first.Page.NextCursor}");
        second.Items.Select(t => t.Slug).ShouldBe(["jacaranda-games", "kmsa"]);

        var last = await ListAsync($"?limit=2&cursor={second.Page.NextCursor}");
        last.Items.Select(t => t.Slug).ShouldBe(["sunbird-juniors"]);
        last.Page.ShouldBe(new(null, false));

        (await ListAsync("?status=ACTIVE")).Items.Select(t => t.Slug).ShouldBe(["kmsa", "sunbird-juniors"]);
        (await ListAsync()).Items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task List_items_have_no_settings()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The list runs under no tenant, and settings are tenant-owned (S02-T4).
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("items").EnumerateArray().ShouldHaveSingleItem()
            .EnumerateObject().Select(p => p.Name)
            .ShouldBe(["id", "slug", "legalName", "displayName", "countryCode", "status", "createdAt", "updatedAt", "version"]);
    }

    [Fact]
    public async Task List_is_audited_with_its_filter_and_no_tenant()
    {
        await ListAsync("?status=SUSPENDED");

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.read"),
            e => e.TenantId.ShouldBeNull(),
            e => e.EntityType.ShouldBeNull());
        JsonDocument.Parse(entry.Metadata.ShouldNotBeNull()).RootElement.GetProperty("status").GetString().ShouldBe("SUSPENDED");
    }

    [Theory]
    [InlineData("status=active", "status")]
    [InlineData("status=CLOSED", "status")]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("limit=ten", "limit")]
    [InlineData("cursor=kmsa", "cursor")]
    public async Task Bad_query_parameters_are_named(string query, string field)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}?{query}", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        CreateTenantTests.Errors(problem).ShouldBe([(field, "VALIDATION_INVALID_QUERY")]);
    }

    private async Task<ListTenantsResponse> ListAsync(string query = "")
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}{query}", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListTenantsResponse>(Json, Ct)).ShouldNotBeNull();
    }

    /// <summary>A platform staff member allowed this endpoint's actions (the base class resets both before each test).</summary>
    private HttpClient CreateClient()
    {
        App.Authorization.Allow(TenancyActions.TenantRead);
        return App.CreateClient().AsUser(TenantTestData.StaffId);
    }
}
