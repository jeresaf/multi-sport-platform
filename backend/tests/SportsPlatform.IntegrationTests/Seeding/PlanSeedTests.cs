using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Seed.Configuration;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary><c>tenancy.configuration.plans</c> on the test database, which the fixture has already seeded (S04 D2).</summary>
public sealed class PlanSeedTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Running_the_plan_seed_twice_changes_nothing()
    {
        var before = await SnapshotAsync();

        var first = await RunAsync(App);
        await Fixture.ResetAsync(); // Forget the runs, keep the data: the seeder runs once more.
        var second = await RunAsync(App);
        var third = await RunAsync(App);

        first.Skipped.ShouldBeFalse();
        first.Outcome.ShouldBe(SeedOutcome.None);
        second.Outcome.ShouldBe(SeedOutcome.None);
        third.Skipped.ShouldBeTrue();
        (await SnapshotAsync()).ShouldBe(before); // xmin included: no row was even rewritten.
    }

    [Fact]
    public async Task Empty_tables_get_every_plan_and_value_as_the_system_actor()
    {
        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "DELETE FROM tenancy.plan_entitlements; DELETE FROM tenancy.plans");

            var result = await RunAsync(App);

            result.Outcome.ShouldBe(new SeedOutcome(2, 0, 0));
            (await QueryAsync(DatabaseRoles.App, "SELECT count(*), min(created_by::text) FROM tenancy.plan_entitlements",
                reader => (reader.GetInt64(0), reader.GetString(1))))
                .ShouldHaveSingleItem().ShouldBe((TenancyPlans.All.Sum(plan => (long)plan.Values.Count), SystemActor.Id.ToString()));
        }
        finally
        {
            await Fixture.RestoreSeedDataAsync();
        }
    }

    [Fact]
    public async Task A_changed_plan_is_updated_and_a_dropped_plan_deactivated_not_deleted_until_declared_again()
    {
        try
        {
            var values = TenancyPlans.Standard.Values.ToDictionary(StringComparer.Ordinal);
            values[TenancyEntitlements.MaxTeams.Code] = EntitlementValue.Quota(150);
            await using var edited = WithPlans(new PlanDefinition("STANDARD", "Standard 2027", 1, values));

            var result = await RunAsync(edited);

            result.Outcome.ShouldBe(new SeedOutcome(0, 1, 1));
            (await PlansAsync()).ShouldBe([("PRO", "Pro", false), ("STANDARD", "Standard 2027", true)]);
            (await ValueAsync("STANDARD", "MAX_TEAMS")).ShouldBe("""{"limit": 150}""");
            (await ValueAsync("PRO", "MAX_TEAMS")).ShouldBe("""{"limit": 2000}""");
        }
        finally
        {
            await Fixture.RestoreSeedDataAsync();
        }

        (await PlansAsync()).ShouldBe([("PRO", "Pro", true), ("STANDARD", "Standard", true)]);
        (await ValueAsync("STANDARD", "MAX_TEAMS")).ShouldBe("""{"limit": 120}""");
    }

    [Fact]
    public void The_checksum_changes_with_the_plans()
    {
        var renamed = new PlanDefinition("PRO", "Pro 2027", 2, TenancyPlans.Pro.Values);

        new PlanSeeder(new PlanCatalogue(TenancyPlans.All)).Checksum
            .ShouldBe(new PlanSeeder(new PlanCatalogue(TenancyPlans.All)).Checksum);
        new PlanSeeder(new PlanCatalogue([TenancyPlans.Standard, renamed])).Checksum
            .ShouldNotBe(new PlanSeeder(new PlanCatalogue(TenancyPlans.All)).Checksum);
    }

    private static async Task<SeederResult> RunAsync(WebApplicationFactory<Program> app)
    {
        var report = await app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Configuration), Ct);
        report.IsRefused.ShouldBeFalse();
        return report.Seeders.Single(s => s.Key == PlanSeeder.SeederKey);
    }

    // The test app with other plans: same database, no outbox processor.
    private WebApplicationFactory<Program> WithPlans(params PlanDefinition[] plans) => App.WithWebHostBuilder(builder => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
        .ConfigureTestServices(services => services.AddSingleton<IPlanCatalogue>(new PlanCatalogue(plans))));

    private Task<List<(string Code, string Name, bool IsActive)>> PlansAsync() => QueryAsync(
        DatabaseRoles.App,
        "SELECT code, name, is_active FROM tenancy.plans ORDER BY code",
        reader => (reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));

    private async Task<string> ValueAsync(string plan, string entitlement) => (await QueryAsync(
        DatabaseRoles.App,
        "SELECT e.value::text FROM tenancy.plan_entitlements e JOIN tenancy.plans p ON p.id = e.plan_id " +
        $"WHERE p.code = '{plan}' AND e.entitlement_code = '{entitlement}'",
        reader => reader.GetString(0))).ShouldHaveSingleItem();

    /// <summary>Every plan row as text, with its <c>xmin</c>: any write changes it.</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        var rows = new List<string>();
        foreach (var table in PostgresFixture.PlanTables)
        {
            var tableRows = await QueryAsync(
                DatabaseRoles.App, $"SELECT '{table}' || ' ' || xmin::text || ' ' || t::text FROM tenancy.{table} t ORDER BY id",
                reader => reader.GetString(0));
            tableRows.ShouldNotBeEmpty(table);
            rows.AddRange(tableRows);
        }

        return rows;
    }
}
