using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Audit;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary>The seed runner with the test seeders (<see cref="TestSeederRegistration"/>), on the test database.</summary>
public sealed class SeedRunnerTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _referenceSeeders = [ProbeReferenceSeeder.SeederKey, "test.reference.second"];

    [Theory]
    [InlineData(SeedCategory.Demo)]
    [InlineData(SeedCategory.Test)]
    public async Task Demo_and_test_seeding_are_refused_in_Production(SeedCategory category)
    {
        await using var production = InEnvironment("Production");

        var report = await Runner(production).RunAsync(new SeedRequest(category), Ct);

        report.IsRefused.ShouldBeTrue();
        report.Seeders.ShouldBeEmpty();
        App.Seeds.Runs.ShouldBeEmpty(); // Not even the REFERENCE prerequisites.
        (await SeedRunsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_seeding_runs_in_Production()
    {
        await using var production = InEnvironment("Production");

        var report = await Runner(production).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

        report.IsRefused.ShouldBeFalse();
        App.Seeds.Runs.ShouldBe(_referenceSeeders);
        (await SeedRunsAsync()).ShouldAllBe(run => run.Environment == "Production");
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task Reset_is_refused_outside_Development(string environment)
    {
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct);
        await using var host = InEnvironment(environment);

        var report = await Runner(host).RunAsync(new SeedRequest(SeedCategory.Reference, Reset: true), Ct);

        report.IsRefused.ShouldBeTrue();
        App.Seeds.Runs.ShouldBeEmpty();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Reset_in_Development_drops_every_module_schema_and_migrates_again()
    {
        await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct); // Append-only: no TRUNCATE.
        await using var development = InEnvironment("Development");

        var report = await Runner(development).RunAsync(new SeedRequest(SeedCategory.Reference, Reset: true), Ct);

        report.IsRefused.ShouldBeFalse();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
        (await SeedRunsAsync()).Select(run => run.SeederKey).ShouldBe(_referenceSeeders); // Only the runs after the reset.
        (await ProbeCodesAsync()).ShouldBe(["KE", "UG"]);
    }

    [Fact]
    public async Task A_category_runs_after_its_prerequisites_ordered_by_category_order_and_key()
    {
        var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        string[] expected = [.. _referenceSeeders, "test.configuration.probe", "test.demo.probe"];
        App.Seeds.Runs.ShouldBe(expected);
        report.Seeders.Select(s => s.Key).ShouldBe(expected);
        report.Seeders.Select(s => s.Category).ShouldBe(
            [SeedCategory.Reference, SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Demo]);
    }

    [Fact]
    public async Task A_reference_seeder_writes_its_read_only_table_as_the_owner_and_the_run_is_recorded()
    {
        App.Actor.ActAsUser(Guid.CreateVersion7()); // The seed connection stamps the current actor.

        var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

        report.Seeders[0].Outcome.ShouldBe(new SeedOutcome(2, 0, 0));
        var probes = await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, code, created_at, created_by FROM conventions_test.reference_probes ORDER BY code",
            reader => (Id: reader.GetGuid(0), Code: reader.GetString(1), CreatedAt: reader.GetFieldValue<DateTimeOffset>(2), CreatedBy: reader.GetGuid(3)));
        probes.Select(p => p.Code).ShouldBe(["KE", "UG"]);
        probes.Select(p => p.Id).ShouldBe([ProbeReferenceSeeder.IdFor("KE"), ProbeReferenceSeeder.IdFor("UG")]);
        probes.ShouldAllBe(p => p.CreatedAt == App.Clock.GetUtcNow() && p.CreatedBy == App.Actor.ActorId);

        var run = (await SeedRunsAsync()).First();
        run.ShouldBe(new SeedRunRow(
            ProbeReferenceSeeder.SeederKey, "REFERENCE", "UG,KE", "Testing", 2, 0, 0,
            App.Clock.GetUtcNow(), App.Clock.GetUtcNow(), App.Actor.ActorId));
    }

    [Fact]
    public async Task Running_again_with_unchanged_checksums_skips_every_seeder()
    {
        await Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        var second = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        second.Seeders.ShouldAllBe(s => s.Skipped && s.Outcome == SeedOutcome.None);
        App.Seeds.Runs.Count.ShouldBe(4);
        (await SeedRunsAsync()).Count.ShouldBe(4);
    }

    [Fact]
    public async Task A_changed_checksum_runs_that_seeder_again()
    {
        await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);
        App.Seeds.ProbeCodes = ["UG", "KE", "TZ"];

        var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

        report.Seeders.Select(s => (s.Key, s.Skipped)).ShouldBe([(ProbeReferenceSeeder.SeederKey, false), ("test.reference.second", true)]);
        report.Seeders[0].Outcome.ShouldBe(new SeedOutcome(1, 0, 0));
        (await ProbeCodesAsync()).ShouldBe(["KE", "TZ", "UG"]);
        (await SeedRunsAsync()).Select(run => run.Checksum).ShouldBe(["UG,KE", "v1", "UG,KE,TZ"]);
    }

    [Fact]
    public async Task A_seeder_that_fails_stops_the_run_and_records_nothing_for_itself()
    {
        App.Seeds.FailingKey = "test.configuration.probe";

        await Should.ThrowAsync<InvalidOperationException>(() => Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct));

        App.Seeds.Runs.ShouldBe(_referenceSeeders);
        (await SeedRunsAsync()).Select(run => run.SeederKey).ShouldBe(_referenceSeeders);
    }

    [Fact]
    public async Task A_demo_seeder_gets_no_owner_context()
    {
        App.Seeds.DemoAsksForOwnerContext = true;

        await Should.ThrowAsync<InvalidOperationException>(() => Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct));

        (await SeedRunsAsync()).ShouldNotContain(run => run.Category == "DEMO");
    }

    [Theory]
    [InlineData(DatabaseRoles.Migrator, "UPDATE platform.seed_runs SET checksum = 'changed'", TableAccessMigrationExtensions.AppendOnlyViolationSqlState)]
    [InlineData(DatabaseRoles.Migrator, "DELETE FROM platform.seed_runs", TableAccessMigrationExtensions.AppendOnlyViolationSqlState)]
    [InlineData(DatabaseRoles.Migrator, "TRUNCATE platform.seed_runs", TableAccessMigrationExtensions.AppendOnlyViolationSqlState)]
    [InlineData(DatabaseRoles.App, "UPDATE platform.seed_runs SET checksum = 'changed'", PostgresErrorCodes.InsufficientPrivilege)]
    [InlineData(DatabaseRoles.Platform, "DELETE FROM platform.seed_runs", PostgresErrorCodes.InsufficientPrivilege)]
    public async Task Seed_runs_cannot_be_changed(string role, string sql, string sqlState)
    {
        await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(role, sql));

        exception.SqlState.ShouldBe(sqlState);
        (await SeedRunsAsync()).Count.ShouldBe(2);
    }

    private static SeedRunner Runner(WebApplicationFactory<Program> app) => app.Services.GetRequiredService<SeedRunner>();

    // The test app in another environment: same database, actor, clock and test seeders. No outbox processor, no
    // startup migration (Development's appsettings turn it on).
    private WebApplicationFactory<Program> InEnvironment(string environment) => App.WithWebHostBuilder(builder => builder
        .UseEnvironment(environment)
        .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Outbox:Enabled"] = "false",
            [StartupMigration.SettingKey] = "false",
        })));

    private Task<List<string>> ProbeCodesAsync() => QueryAsync(
        DatabaseRoles.App, "SELECT code FROM conventions_test.reference_probes ORDER BY code", reader => reader.GetString(0));

    private Task<List<SeedRunRow>> SeedRunsAsync() => QueryAsync(
        DatabaseRoles.App,
        "SELECT seeder_key, category, checksum, environment, inserted, updated, deactivated, started_at, completed_at, created_by " +
        "FROM platform.seed_runs ORDER BY id",
        reader => new SeedRunRow(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
            reader.GetInt32(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8), reader.GetGuid(9)));

    private sealed record SeedRunRow(
        string SeederKey, string Category, string Checksum, string Environment, int Inserted, int Updated, int Deactivated,
        DateTimeOffset StartedAt, DateTimeOffset CompletedAt, Guid CreatedBy);
}
