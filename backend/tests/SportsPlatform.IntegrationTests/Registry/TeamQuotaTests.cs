using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// <c>MAX_TEAMS</c> (S07 D14; owner, Q50; T3 acceptance 3): teams in <c>ACTIVE</c>, <c>INACTIVE</c> or
/// <c>SUSPENDED</c> take places, disbanded and archived ones don't; a create, a reform or an unarchive to a status that
/// counts at the limit is <c>403 ENTITLEMENT_QUOTA_EXCEEDED</c> ("Your plan allows N teams.") under
/// <c>registry.teams:&lt;tenantId&gt;</c>, so two of them at the limit don't both pass; disbanding frees a place; the
/// usage shows on the tenant's subscription read. KMSA's limit comes from an override.
/// </summary>
public sealed class TeamQuotaTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _subscriptionPath = "/api/admin/v1/tenant/subscription";

    [Fact]
    public async Task Teams_that_count_take_places_and_a_create_at_the_limit_is_refused()
    {
        var (kmsa, owner) = await KmsaAsync(limit: 3);
        await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC", status: TeamStatus.Inactive);
        await SaveTeamAsync(App.Services, kmsa, "Kireka Kingfishers FC", status: TeamStatus.Suspended);
        await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC", status: TeamStatus.Disbanded);
        await SaveTeamAsync(App.Services, kmsa, "Mutundwe Weavers FC", status: TeamStatus.Archived);
        (await TeamUsageAsync(owner)).ShouldBe((2, 3));

        await CreateTeamAsync(owner, NtindaSwiftsMen());
        (await TeamUsageAsync(owner)).ShouldBe((3, 3));
        var next = NtindaSwiftsMen();
        next["name"] = "Kisaasi Kestrels FC";
        using var refused = await owner.PostAsJsonAsync(TeamsPath, next, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 3 teams.");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var denied = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "team.create" && e.Outcome != AuditOutcome.Success);
        (denied.TenantId, denied.ErrorCode).ShouldBe((kmsa, "ENTITLEMENT_QUOTA_EXCEEDED"));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(5);
    }

    // D14, D21: the count and the save happen under the teams' lock, so the second create sees the first's team.
    [Fact]
    public async Task Two_creates_at_the_limit_dont_both_pass()
    {
        var (kmsa, owner) = await KmsaAsync(limit: 1);
        var kestrels = NtindaSwiftsMen();
        kestrels["name"] = "Kisaasi Kestrels FC";

        var responses = await Task.WhenAll(
            owner.PostAsJsonAsync(TeamsPath, NtindaSwiftsMen(), Ct),
            owner.PostAsJsonAsync(TeamsPath, kestrels, Ct));

        responses.Select(r => r.StatusCode).OrderBy(s => s).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Forbidden]);
        await responses.Single(r => r.StatusCode == HttpStatusCode.Forbidden)
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(1);
    }

    // T3 acceptance 3: disbanding frees a place, and reforming takes one back.
    [Fact]
    public async Task Disbanding_frees_a_place_and_a_reform_at_the_limit_is_refused()
    {
        var (kmsa, owner) = await KmsaAsync(limit: 1);
        var swifts = (await CreateTeamAsync(owner, NtindaSwiftsMen())).GetProperty("id").GetGuid();
        await MoveTeamAsync(owner, swifts, "DISBANDED", "Not enough players this season");
        (await TeamUsageAsync(owner)).ShouldBe((0, 1));
        var kestrels = NtindaSwiftsMen();
        kestrels["name"] = "Kisaasi Kestrels FC";
        await CreateTeamAsync(owner, kestrels);

        using var reform = await owner.PostAsJsonAsync(TeamStatusPath(swifts), new { status = "ACTIVE", reason = "Reformed for the new season" }, Ct);

        var problem = await reform.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 1 teams.");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var denied = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Single(e => e.ActionCode == "team.status.change" && e.Outcome != AuditOutcome.Success);
        (denied.EntityId, denied.ErrorCode).ShouldBe((swifts, "ENTITLEMENT_QUOTA_EXCEEDED"));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Team>().AsNoTracking().Include(t => t.StatusHistory).SingleAsync(t => t.Id == swifts, Ct);
        (saved.Status, saved.StatusHistory.Count).ShouldBe((TeamStatus.Disbanded, 2));
    }

    // D14: unarchiving to a status that counts takes a place; unarchiving a disbanded team doesn't.
    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("SUSPENDED", true)]
    [InlineData("DISBANDED", false)]
    public async Task An_unarchive_at_the_limit_is_refused_only_when_it_takes_a_place(string before, bool takesAPlace)
    {
        var (_, owner) = await KmsaAsync(limit: 1);
        var swifts = (await CreateTeamAsync(owner, NtindaSwiftsMen())).GetProperty("id").GetGuid();
        if (before != "ACTIVE")
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveTeamAsync(owner, swifts, before, "Not enough players this season");
        }

        App.Clock.Advance(TimeSpan.FromMinutes(1));
        await MoveTeamAsync(owner, swifts, "ARCHIVED", "Folded into the first team");
        var kestrels = NtindaSwiftsMen();
        kestrels["name"] = "Kisaasi Kestrels FC";
        await CreateTeamAsync(owner, kestrels);
        App.Clock.Advance(TimeSpan.FromMinutes(1));

        using var unarchive = await owner.PostAsJsonAsync(TeamStatusPath(swifts), new { status = before, reason = "Archived by mistake" }, Ct);

        if (takesAPlace)
        {
            await unarchive.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        }
        else
        {
            unarchive.StatusCode.ShouldBe(HttpStatusCode.OK, await unarchive.Content.ReadAsStringAsync(Ct));
        }
    }

    // Moves between statuses that count take no new place: at the limit they still pass.
    [Fact]
    public async Task Moves_between_statuses_that_count_pass_at_the_limit()
    {
        var (_, owner) = await KmsaAsync(limit: 1);
        var swifts = (await CreateTeamAsync(owner, NtindaSwiftsMen())).GetProperty("id").GetGuid();

        await MoveTeamAsync(owner, swifts, "SUSPENDED", "League fees unpaid");
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        await MoveTeamAsync(owner, swifts, "INACTIVE", "Fees paid; no fixtures yet");
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        (await MoveTeamAsync(owner, swifts, "ACTIVE", "Back in the league")).GetProperty("status").GetString().ShouldBe("ACTIVE");
    }

    // D14, D21: two reforms with one place left queue on the teams' lock, so only one passes.
    [Fact]
    public async Task Two_reforms_with_one_place_left_dont_both_pass()
    {
        var (kmsa, owner) = await KmsaAsync(limit: 1);
        var swifts = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", status: TeamStatus.Disbanded);
        var kestrels = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC", status: TeamStatus.Disbanded);

        var responses = await Task.WhenAll(
            owner.PostAsJsonAsync(TeamStatusPath(swifts.Id), new { status = "ACTIVE", reason = "Reformed" }, Ct),
            owner.PostAsJsonAsync(TeamStatusPath(kestrels.Id), new { status = "ACTIVE", reason = "Reformed" }, Ct));

        responses.Select(r => r.StatusCode).OrderBy(s => s).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Forbidden]);
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(t => t.Status == TeamStatus.Active, Ct)).ShouldBe(1);
    }

    /// <summary>KMSA with <see cref="TestballFull"/> enabled, <c>MAX_TEAMS</c> overridden to <paramref name="limit"/>, and its owner's client.</summary>
    private async Task<(Guid Tenant, HttpClient Owner)> KmsaAsync(int limit)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await ExecuteAsync(
            DatabaseRoles.App,
            $"SELECT set_config('app.tenant_id', '{kmsa}', false); " +
            "INSERT INTO tenancy.tenant_entitlement_overrides (id, tenant_id, entitlement_code, value, reason, " +
            "created_at, created_by, updated_at, updated_by) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{kmsa}', 'MAX_TEAMS', '{{\"limit\": {limit}}}', 'Pilot agreement: a few teams', " +
            $"now(), '{TenantTestData.StaffId}', now(), '{TenantTestData.StaffId}')");
        App.Services.GetRequiredService<TenantAccessCache>().Clear(kmsa);
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        return (kmsa, App.ClientFor(grace.UserId, kmsa));
    }

    // The MAX_TEAMS row of the tenant's subscription read: used and limit.
    private static async Task<(int? Used, int? Limit)> TeamUsageAsync(HttpClient owner)
    {
        using var response = await owner.GetAsync(_subscriptionPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var usage = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("usage").EnumerateArray()
            .Single(u => u.GetProperty("code").GetString() == "MAX_TEAMS");
        usage.GetProperty("measured").GetBoolean().ShouldBeTrue();
        return (usage.GetProperty("used").GetInt32(), usage.GetProperty("limit").GetInt32());
    }
}
