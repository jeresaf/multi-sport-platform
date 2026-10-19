using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>
/// S03 D3, D4: a caller who is not signed in is the anonymous actor; any action that needs a user answers
/// <c>401 AUTH_UNAUTHENTICATED</c>, not 403, and a denial of an audited action keeps one DENIED row per request.
/// </summary>
public sealed class AnonymousRequestTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // S03 acceptance: an anonymous denial on an audited endpoint writes a DENIED entry with actor kind ANONYMOUS.
    [Fact]
    public async Task Anonymous_denial_of_an_audited_platform_action_is_audited_as_anonymous()
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient();

        using var response = await client.PostAsJsonAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), TenantTestData.CreateBody(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        App.Authorization.Requests.ShouldBeEmpty();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.create"),
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("AUTH_UNAUTHENTICATED"),
            e => e.ActorKind.ShouldBe(ActorKind.Anonymous),
            e => e.ActorId.ShouldBe(AnonymousActor.Id),
            e => e.TenantId.ShouldBeNull());
    }

    [Theory]
    [InlineData("/api/platform/v1/tenants")]
    [InlineData("/api/admin/v1/tenant/settings")]
    [InlineData("/api/account/v1/me")]
    public async Task Anonymous_read_is_unauthenticated(string path)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.CreateVersion7().ToString());

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    // D4: 401 before the route's tenant is looked up, so an unknown tenant answers like a real one.
    [Fact]
    public async Task Anonymous_request_on_a_route_tenant_is_unauthenticated_whether_or_not_the_tenant_exists()
    {
        App.Authorization.AllowAll();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var client = App.CreateClient();
        var reason = new { reason = "Council closed for the school holidays" };

        using var known = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{kmsa}", UriKind.Relative), Ct);
        using var unknown = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{Guid.CreateVersion7()}", UriKind.Relative), Ct);
        using var suspend = await client.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{Guid.CreateVersion7()}/suspend", UriKind.Relative), reason, Ct);

        await known.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        await suspend.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.Select(e => e.ActionCode).ShouldBe(["tenant.read", "tenant.read", "tenant.suspend"], ignoreOrder: true);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Denied && e.ErrorCode == "AUTH_UNAUTHENTICATED" && e.TenantId == null);
    }

    [Fact]
    public async Task Public_reads_stay_anonymous_and_open()
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/public/v1/reference/countries", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
