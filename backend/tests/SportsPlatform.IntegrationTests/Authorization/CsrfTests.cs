using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>
/// S03 D7: a state-changing request to an account, admin or platform endpoint needs <c>X-CSRF: 1</c>, sign-in
/// included; the refusal comes before tenant resolution and authorization and is audited. Reads don't need it.
/// </summary>
public sealed class CsrfTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "daniel.kato@kmsa.example";

    // S03 acceptance: a mutation without X-CSRF → 403 AUTH_CSRF_REQUIRED; with it and a valid session → allowed.
    [Fact]
    public async Task A_change_without_the_header_is_refused_and_with_it_the_session_is_allowed()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();
        App.Authorization.Requests.Clear();

        using var refused = await client.WithoutCsrfHeader().PostAsync(AccountTestData.LogoutPath, content: null, Ct);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        using var allowed = await client.PostAsync(AccountTestData.LogoutPath, content: null, Ct);

        await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        App.Authorization.Requests.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            r => r.Action.ShouldBe(IdentityActions.AccountLogout),
            r => r.Actor.ActorId.ShouldBe(userId));
    }

    [Fact]
    public async Task Sign_in_without_the_header_is_refused_and_audited()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient().WithoutCsrfHeader();

        using var response = await client.LoginAsync(_email, password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("account.login"),
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("AUTH_CSRF_REQUIRED"),
            e => e.ActorKind.ShouldBe(ActorKind.Anonymous));
    }

    [Fact]
    public async Task Admin_and_platform_changes_need_the_header_before_tenant_or_permission_checks()
    {
        App.Authorization.AllowAll();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId, kmsa).WithoutCsrfHeader();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());

        using var platform = await client.PostAsJsonAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), TenantTestData.CreateBody(), Ct);
        using var admin = await client.PatchAsJsonAsync(new Uri("/api/admin/v1/tenant/settings", UriKind.Relative), new { }, Ct);
        using var read = await client.GetAsync(new Uri("/api/admin/v1/tenant/settings", UriKind.Relative), Ct);

        await platform.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        await admin.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Authorization.Requests.ShouldHaveSingleItem().Action.ShouldBe(TenancyActions.TenantSettingsRead);
    }

    [Fact]
    public async Task An_unknown_route_stays_404_without_the_header()
    {
        using var client = App.CreateClient().WithoutCsrfHeader();

        using var response = await client.PostAsync(new Uri("/api/admin/v1/nothing-here", UriKind.Relative), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_ROUTE_NOT_FOUND");
    }
}
