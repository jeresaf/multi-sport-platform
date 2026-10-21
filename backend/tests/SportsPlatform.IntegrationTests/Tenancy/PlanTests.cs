using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Features.Plans.ListPlans;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>The seeded plans (S04 D2) and <c>GET /api/platform/v1/plans</c> (<c>plan.read</c>, D16).</summary>
public sealed class PlanTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _plansPath = "/api/platform/v1/plans";

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    // S04-T1 acceptance: the seeded values match TenancyPlans, which AuthorizationEntitlementTableTests matches with
    // authorization.md's table.
    [Fact]
    public async Task The_seeded_plans_have_exactly_the_values_of_the_code_with_deterministic_IDs()
    {
        var plans = await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, code, name, rank, is_active FROM tenancy.plans ORDER BY rank",
            reader => (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4)));
        var values = await QueryAsync(
            DatabaseRoles.App,
            "SELECT e.id, p.code, e.entitlement_code, e.value::text FROM tenancy.plan_entitlements e " +
            "JOIN tenancy.plans p ON p.id = e.plan_id ORDER BY p.code, e.entitlement_code",
            reader => (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), JsonSerializer.Deserialize<EntitlementValue>(reader.GetString(3))!));

        plans.ShouldBe(TenancyPlans.All.Select(plan => (Plan.IdFor(plan.Code), plan.Code, plan.Name, plan.Rank, true)));
        values.ShouldBe(
            TenancyPlans.All
                .SelectMany(plan => plan.Values.Select(entry => (PlanEntitlement.IdFor(plan.Code, entry.Key), plan.Code, entry.Key, entry.Value)))
                .OrderBy(row => row.Code, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal),
            ignoreOrder: false);
    }

    [Fact]
    public async Task The_runtime_role_cannot_change_a_plan()
    {
        var refused = await Should.ThrowAsync<Npgsql.PostgresException>(() =>
            ExecuteAsync(DatabaseRoles.App, "UPDATE tenancy.plan_entitlements SET value = '{\"limit\":null}'"));

        refused.SqlState.ShouldBe(Npgsql.PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Lists_the_plans_by_rank_with_every_entitlement_by_code_and_is_audited()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri(_plansPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plans = (await response.Content.ReadFromJsonAsync<ListPlansResponse>(Json, Ct)).ShouldNotBeNull().Items;
        plans.Select(p => (p.Code, p.Name, p.Rank)).ShouldBe([("STANDARD", "Standard", 1), ("PRO", "Pro", 2)]);
        var declared = new DeclaredEntitlements(ModuleRegistry.All).All;
        foreach (var plan in plans)
        {
            var definition = TenancyPlans.All.Single(p => p.Code == plan.Code);
            plan.Entitlements.Select(e => e.Code).ShouldBe(declared.Select(e => e.Code).Order(StringComparer.Ordinal));
            plan.Entitlements.ShouldAllBe(e => e.Value == definition.Values[e.Code]);
        }

        var staffUsers = plans[0].Entitlements.Single(e => e.Code == TenancyEntitlements.MaxStaffUsers.Code);
        staffUsers.ShouldSatisfyAllConditions(
            e => e.Type.ShouldBe(EntitlementType.Quota),
            e => e.Unit.ShouldBe("users"),
            e => e.Default.ShouldBe(EntitlementValue.Quota(0)),
            e => e.EnforcedBy.ShouldBe(TenancyEntitlements.MaxStaffUsers.EnforcedBy));
        plans[1].Entitlements.Single(e => e.Code == TenancyEntitlements.Reports.Code).Levels.ShouldBe(["BASIC", "ADVANCED"]);

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("plan.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBeNull());
    }

    [Fact]
    public async Task Values_have_the_one_typed_JSON_shape()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri(_plansPath, UriKind.Relative), Ct);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var pro = document.RootElement.GetProperty("items")[1].GetProperty("entitlements").EnumerateArray()
            .ToDictionary(e => e.GetProperty("code").GetString()!, e => e.GetProperty("value").GetRawText());
        pro["MAX_ENABLED_SPORTS"].ShouldBe("""{"limit":null}""");
        pro["MAX_STAFF_USERS"].ShouldBe("""{"limit":1000}""");
        pro["CUSTOM_DOMAIN"].ShouldBe("""{"enabled":true}""");
        pro["BRANDING"].ShouldBe("""{"level":"FULL"}""");
    }

    [Fact]
    public async Task An_inactive_plan_is_not_listed()
    {
        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "UPDATE tenancy.plans SET is_active = false WHERE code = 'PRO'");

            (await ListAsync()).Select(plan => plan.Code).ShouldBe(["STANDARD"]);
        }
        finally
        {
            await Fixture.RestoreSeedDataAsync();
        }
    }

    // A declared entitlement the seed hasn't written yet has no value, not a made-up default (AR-30; PROGRESS: T4).
    [Fact]
    public async Task A_declared_entitlement_without_a_stored_value_is_listed_with_a_null_value()
    {
        try
        {
            await ExecuteAsync(
                DatabaseRoles.Migrator,
                "DELETE FROM tenancy.plan_entitlements WHERE entitlement_code = 'CUSTOM_DOMAIN' " +
                "AND plan_id = (SELECT id FROM tenancy.plans WHERE code = 'PRO')");

            var plans = await ListAsync();

            plans.Single(p => p.Code == "PRO").Entitlements.Single(e => e.Code == "CUSTOM_DOMAIN").Value.ShouldBeNull();
            plans.Single(p => p.Code == "STANDARD").Entitlements.Single(e => e.Code == "CUSTOM_DOMAIN").Value
                .ShouldBe(TenancyPlans.Standard.Values["CUSTOM_DOMAIN"]);
        }
        finally
        {
            await Fixture.RestoreSeedDataAsync();
        }
    }

    // Seeders never delete: a value kept for an entitlement no longer declared is stored but never listed.
    [Fact]
    public async Task A_stored_value_for_an_entitlement_not_declared_is_not_listed()
    {
        const string retired = "RETIRED_FEATURE";
        try
        {
            await ExecuteAsync(
                DatabaseRoles.Migrator,
                "INSERT INTO tenancy.plan_entitlements (id, plan_id, entitlement_code, value, created_at, created_by, updated_at, updated_by) " +
                $"SELECT gen_random_uuid(), id, '{retired}', '{{\"enabled\":true}}', now(), '{Guid.Empty}', now(), '{Guid.Empty}' " +
                "FROM tenancy.plans WHERE code = 'PRO'");

            var plans = await ListAsync();

            plans.ShouldAllBe(plan => plan.Entitlements.All(e => e.Code != retired));
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, $"DELETE FROM tenancy.plan_entitlements WHERE entitlement_code = '{retired}'");
        }
    }

    private async Task<IReadOnlyList<PlanResponse>> ListAsync()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri(_plansPath, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListPlansResponse>(Json, Ct)).ShouldNotBeNull().Items;
    }

    /// <summary>A platform staff member allowed this endpoint's action (the base class resets it before each test).</summary>
    private HttpClient CreateClient()
    {
        App.Authorization.Allow(TenancyActions.PlanRead);
        return App.CreateClient().AsUser(TenantTestData.StaffId);
    }
}
