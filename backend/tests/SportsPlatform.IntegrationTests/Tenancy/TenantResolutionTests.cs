using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Tenant resolution (S02-T3a): the admin API takes the tenant from <c>X-Tenant-Id</c> (membership required) and
/// refuses a blocked tenant; platform tenant routes run under the route's tenant (D2). Refusals of audited actions
/// are audited with the tenant when there is one.
/// </summary>
public sealed class TenantResolutionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _tenantHeader = "X-Tenant-Id";

    private static readonly Guid _secretaryId = new("0192a1b2-0000-7000-8000-00000000c001");

    [Fact]
    public async Task Admin_request_without_a_tenant_header_is_refused_and_audited()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient().AsUser(_secretaryId, kmsa);

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("fixture.create"),
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("AUTH_TENANT_FORBIDDEN"),
            e => e.ActorKind.ShouldBe(ActorKind.User),
            e => e.ActorId.ShouldBe(_secretaryId),
            e => e.TenantId.ShouldBeNull());
    }

    [Theory]
    [InlineData("kmsa")]
    [InlineData("0192a1b2-0000-7000-8000-00000000e999")] // a tenant the user is not a member of
    public async Task Admin_request_for_a_tenant_the_user_is_not_a_member_of_is_refused(string header)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient().AsUser(_secretaryId, kmsa);
        client.DefaultRequestHeaders.Add(_tenantHeader, header);

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("AUTH_TENANT_FORBIDDEN"),
            e => e.TenantId.ShouldBeNull());
    }

    [Fact]
    public async Task Admin_request_from_another_tenant_member_is_refused()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient().AsUser(_secretaryId, elssc);
        client.DefaultRequestHeaders.Add(_tenantHeader, kmsa.ToString());

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
    }

    [Fact]
    public async Task Anonymous_admin_request_is_refused_as_the_system_actor()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient();
        client.DefaultRequestHeaders.Add(_tenantHeader, kmsa.ToString());

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.ActorKind.ShouldBe(ActorKind.System),
            e => e.ActorId.ShouldBe(SystemActor.Id));
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Provisioning)] // setup happens in the admin app
    public async Task Member_works_in_a_full_access_tenant_and_the_entry_names_it(TenantStatus status)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", status, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient().AsUser(_secretaryId, kmsa);
        client.DefaultRequestHeaders.Add(_tenantHeader, kmsa.ToString());

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<Guid?>(Ct)).ShouldBe(kmsa);
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(kmsa));
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Archived)]
    public async Task Member_of_a_blocked_tenant_is_refused_and_the_entry_names_it(TenantStatus status)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", status, Ct);
        await using var admin = await AdminTestApp.StartAsync(Fixture, Ct);
        using var client = admin.CreateClient().AsUser(_secretaryId, kmsa);
        client.DefaultRequestHeaders.Add(_tenantHeader, kmsa.ToString());

        using var response = await client.PostAsync(AdminTestApp.FixturesPath, content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("TENANT_BLOCKED"),
            e => e.TenantId.ShouldBe(kmsa));
    }

    [Fact]
    public async Task Platform_route_with_an_unknown_tenant_is_not_found_and_audited()
    {
        App.Authorization.Allow(TenancyActions.TenantRead);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);

        using var response = await client.GetAsync(
            new Uri($"{TenantTestData.TenantsPath}/0192a1b2-0000-7000-8000-00000000e999", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
        App.Authorization.Requests.ShouldBeEmpty(); // refused before authorization
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.ErrorCode.ShouldBe("TENANT_NOT_FOUND"),
            e => e.TenantId.ShouldBeNull());
    }

    [Fact]
    public async Task Platform_route_denial_is_audited_with_the_route_tenant()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);

        using var response = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{kmsa}", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.TenantId.ShouldBe(kmsa));
    }

    [Fact]
    public async Task Platform_route_validation_failure_is_audited_with_the_route_tenant()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        App.Authorization.Allow(TenancyActions.TenantSuspend);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);

        using var response = await client.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{kmsa}/suspend", UriKind.Relative), new { reason = " " }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        (await SingleEntryAsync()).ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.TenantId.ShouldBe(kmsa));
    }

    [Fact]
    public async Task Platform_routes_without_a_route_tenant_ignore_the_tenant_header()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        App.Authorization.Allow(PlatformActions.AuditRead);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId, kmsa);
        client.DefaultRequestHeaders.Add(_tenantHeader, kmsa.ToString());

        // A scope tenant here would make Describe(targetTenantId: elssc) throw (S02-T2b).
        using var response = await client.GetAsync(new Uri($"/api/platform/v1/audit-entries?tenantId={elssc}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SingleEntryAsync()).TenantId.ShouldBe(elssc);
    }

    private async Task<AuditEntry> SingleEntryAsync() => (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
}
