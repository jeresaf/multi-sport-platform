using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;

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

    [Fact]
    public async Task Public_reads_stay_anonymous_and_open()
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/public/v1/reference/countries", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
