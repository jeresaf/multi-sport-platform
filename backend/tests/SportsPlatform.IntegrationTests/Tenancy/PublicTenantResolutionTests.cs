using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Public tenant resolution (S02-T3b, D6, D7): <c>.RequirePublicTenant()</c> endpoints take the tenant from the
/// request host, or from <c>X-Forwarded-Host</c> sent by a trusted proxy; in Development only, from the override.
/// </summary>
public sealed class PublicTenantResolutionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Site_answers_for_the_tenant_of_the_host()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);

        var kmsa = await App.Server.GetAsync(PublicRequests.SitePath, "KMSA.localhost:3000");
        var elssc = await App.Server.GetAsync(PublicRequests.SitePath, "elssc.localhost");

        kmsa.ShouldBeSite("kmsa");
        kmsa.Json.GetProperty("displayName").GetString().ShouldBe("kmsa Sports");
        kmsa.Json.GetProperty("primaryDomain").GetString().ShouldBe("kmsa.localhost");
        kmsa.Json.EnumerateObject().Select(p => p.Name).ShouldBe(["slug", "displayName", "primaryDomain"], ignoreOrder: true);
        kmsa.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        kmsa.Headers.ETag.ToString().ShouldNotBeNullOrEmpty();
        kmsa.Headers.Vary.ToString().ShouldBe("X-Forwarded-Host");
        elssc.ShouldBeSite("elssc");
        elssc.Headers.ETag.ToString().ShouldNotBe(kmsa.Headers.ETag.ToString());
    }

    [Theory]
    [InlineData("nobody.localhost")]
    [InlineData("localhost")]
    [InlineData("kmsa.localhost.evil.example")]
    [InlineData("[::1]:5080")]
    public async Task Unknown_host_is_not_found(string host)
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);

        (await App.Server.GetAsync(PublicRequests.SitePath, host)).ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
    }

    [Theory]
    [InlineData(TenantStatus.Provisioning, true)]
    [InlineData(TenantStatus.Active, false)]
    [InlineData(TenantStatus.Suspended, true)]
    [InlineData(TenantStatus.Archived, true)]
    public async Task Hidden_or_blocked_tenant_is_unavailable(TenantStatus status, bool publicEnabled)
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", status, publicEnabled, withDomain: true, Ct);

        (await App.Server.GetAsync(PublicRequests.SitePath, "kmsa.localhost"))
            .ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");
    }

    [Fact]
    public async Task Reference_lists_answer_on_any_host()
    {
        var response = await App.Server.GetAsync("/api/public/v1/reference/countries", "nobody.example");

        response.Status.ShouldBe(StatusCodes.Status200OK);
        response.Headers.Vary.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Forwarded_host_counts_only_from_a_trusted_proxy()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await using var app = await PublicTestApp.StartAsync(Fixture, TestEnvironment.Name, Ct);

        var fromProxy = await app.Server.GetAsync(
            PublicRequests.SitePath, "api.internal", PublicRequests.Proxy, ("X-Forwarded-Host", "kmsa.localhost:3000"));
        var fromBrowser = await app.Server.GetAsync(
            PublicRequests.SitePath, "api.internal", PublicRequests.Browser, ("X-Forwarded-Host", "kmsa.localhost"));

        fromProxy.ShouldBeSite("kmsa");
        fromBrowser.ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task Without_configured_proxies_no_forwarded_host_counts()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);

        // TestApp lists no proxies, and the framework's loopback defaults are removed.
        var response = await App.Server.GetAsync(
            PublicRequests.SitePath, "api.internal", System.Net.IPAddress.Loopback, ("X-Forwarded-Host", "kmsa.localhost"));

        response.ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task Development_override_names_the_tenant()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "mvule-youth", TenantStatus.Provisioning, publicEnabled: true, withDomain: true, Ct);
        await using var app = await PublicTestApp.StartAsync(Fixture, Environments.Development, Ct);

        var byHeader = await app.Server.GetAsync(PublicRequests.SitePath, "localhost:5080", headers: ("X-Tenant-Slug", "kmsa"));
        var byQuery = await app.Server.GetAsync($"{PublicRequests.SitePath}?tenant=elssc", "kmsa.localhost");
        var unknown = await app.Server.GetAsync(PublicRequests.SitePath, "kmsa.localhost", headers: ("X-Tenant-Slug", "nobody"));
        var provisioning = await app.Server.GetAsync(PublicRequests.SitePath, "localhost", headers: ("X-Tenant-Slug", "mvule-youth"));

        byHeader.ShouldBeSite("kmsa");
        byHeader.Headers.Vary.ToString().ShouldBe("X-Forwarded-Host, X-Tenant-Slug");
        byQuery.ShouldBeSite("elssc");
        unknown.ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
        provisioning.ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task Override_is_ignored_outside_Development(string environment)
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await using var app = await PublicTestApp.StartAsync(Fixture, environment, Ct, ("Platform:BaseDomain", "sports.test"));

        var byHeader = await app.Server.GetAsync(PublicRequests.SitePath, "localhost", headers: ("X-Tenant-Slug", "kmsa"));
        var byQuery = await app.Server.GetAsync($"{PublicRequests.SitePath}?tenant=kmsa", "localhost");

        byHeader.ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
        byHeader.Headers.Vary.ToString().ShouldBe("X-Forwarded-Host");
        byQuery.ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
    }

    // Each change that decides what a host shows clears the host cache, so the next request sees it at once.
    [Fact]
    public async Task Tenant_changes_reach_the_public_site_at_once()
    {
        App.Authorization.AllowAll();
        using var platform = App.CreateClient().AsUser(TenantTestData.StaffId);

        // The miss is cached as unknown until the tenant is created.
        (await SiteAsync()).ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
        using var created = await platform.PostAsJsonAsync(
            TenantTestData.TenantsPath, TenantTestData.CreateBody("kmsa", ("publicEnabled", true)), Ct);
        created.EnsureSuccessStatusCode();
        var tenantId = (await created.Content.ReadFromJsonAsync<JsonIdentity>(Ct)).ShouldNotBeNull().Id;
        (await SiteAsync()).ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");

        (await platform.PostAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/activate", UriKind.Relative), null, Ct))
            .EnsureSuccessStatusCode();
        (await SiteAsync()).ShouldBeSite("kmsa");

        (await platform.PatchAsJsonAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative), new { publicEnabled = false }, Ct))
            .EnsureSuccessStatusCode();
        (await SiteAsync()).ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");

        (await platform.PatchAsJsonAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative), new { publicEnabled = true }, Ct))
            .EnsureSuccessStatusCode();
        (await SiteAsync()).ShouldBeSite("kmsa");

        (await platform.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{tenantId}/suspend", UriKind.Relative), new { reason = "Season fees outstanding" }, Ct))
            .EnsureSuccessStatusCode();
        (await SiteAsync()).ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");

        Task<PublicResponse> SiteAsync() => App.Server.GetAsync(PublicRequests.SitePath, "kmsa.localhost");
    }

    private sealed record JsonIdentity(Guid Id);
}
