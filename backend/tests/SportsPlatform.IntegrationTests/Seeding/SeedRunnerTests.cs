using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Audit;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary>
/// The seed runner with the test seeders (<see cref="TestSeederRegistration"/>), on the test database. Platform's
/// REFERENCE seeders run too; assertions look at the test seeders (<see cref="TestSeeders"/>, <see cref="SeedRunsAsync"/>).
/// </summary>
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
        (await SeedRunsAsync()).Select(run => run.Environment).ShouldBe(["Production", "Production"]);
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
    public async Task The_database_reset_refuses_outside_Development_whoever_calls_it()
    {
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.Services.GetRequiredService<DatabaseReset>().ResetAsync(Ct));

        exception.Message.ShouldContain("only in Development");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Reset_in_Development_drops_every_module_schema_and_migrates_again()
    {
        await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct); // Append-only: no TRUNCATE.
        // Testcontainers may report a non-local Docker host; locality itself is DatabaseResetTests'.
        await using var development = InEnvironment("Development", new() { [DatabaseReset.AllowResetOfKey] = FixtureHost });

        var report = await Runner(development).RunAsync(new SeedRequest(SeedCategory.Reference, Reset: true), Ct);

        report.IsRefused.ShouldBeFalse();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
        (await SeedRunsAsync()).Select(run => run.SeederKey).ShouldBe(_referenceSeeders); // Only the runs after the reset.
        (await ProbeCodesAsync()).ShouldBe(["KE", "UG"]);
        (await QueryAsync(DatabaseRoles.App, "SELECT count(*) FROM platform.countries", reader => reader.GetInt64(0)))
            .ShouldHaveSingleItem().ShouldBeGreaterThan(0); // The real reference data is seeded again.
    }

    [Fact]
    public async Task Reset_in_Development_is_refused_for_a_non_local_database_before_anything_runs()
    {
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct);
        await using var development = InEnvironment("Development", new()
        {
            // Staging credentials exported in a Development shell; never contacted.
            [$"ConnectionStrings:{ConnectionStringNames.Migrator}"] =
                "Host=kmsa-staging.postgres.database.azure.com;Database=sportsplatform;Username=sp_migrator;Password=not-used",
        });

        var report = await Runner(development).RunAsync(new SeedRequest(SeedCategory.Reference, Reset: true), Ct);

        report.Refusal.ShouldNotBeNull().ShouldContain("kmsa-staging.postgres.database.azure.com");
        App.Seeds.Runs.ShouldBeEmpty();
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_second_run_while_one_holds_the_seed_lock_is_refused()
    {
        await using (var other = await OpenMigratorAsync())
        {
            (await AdvisoryAsync(other, "pg_try_advisory_lock")).ShouldBeTrue(); // Another seed run, elsewhere.

            var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

            report.Refusal.ShouldNotBeNull().ShouldContain("in progress");
            App.Seeds.Runs.ShouldBeEmpty();
            (await SeedRunsAsync()).ShouldBeEmpty();
            await AdvisoryAsync(other, "pg_advisory_unlock");
        }

        (await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct)).IsRefused.ShouldBeFalse();
        await ShouldBeUnlockedAsync();
    }

    [Fact]
    public async Task A_category_runs_after_its_prerequisites_ordered_by_category_order_and_key()
    {
        var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        string[] expected = [.. _referenceSeeders, "test.configuration.probe", "test.demo.probe"];
        App.Seeds.Runs.ShouldBe(expected);
        TestSeeders(report).Select(s => s.Key).ShouldBe(expected);
        TestSeeders(report).Select(s => s.Category).ShouldBe(
            [SeedCategory.Reference, SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Demo]);
    }

    [Fact]
    public async Task A_reference_seeder_writes_its_read_only_table_as_the_owner_and_the_run_is_recorded()
    {
        var report = await Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct);

        TestSeeders(report)[0].Outcome.ShouldBe(new SeedOutcome(2, 0, 0));
        var probes = await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, code, created_at, created_by FROM conventions_test.reference_probes ORDER BY code",
            reader => (Id: reader.GetGuid(0), Code: reader.GetString(1), CreatedAt: reader.GetFieldValue<DateTimeOffset>(2), CreatedBy: reader.GetGuid(3)));
        probes.Select(p => p.Code).ShouldBe(["KE", "UG"]);
        probes.Select(p => p.Id).ShouldBe([ProbeReferenceSeeder.IdFor("KE"), ProbeReferenceSeeder.IdFor("UG")]);
        probes.ShouldAllBe(p => p.CreatedAt == App.Clock.GetUtcNow() && p.CreatedBy == SystemActor.Id); // Seeding is system work.

        var run = (await SeedRunsAsync()).First();
        run.ShouldBe(new SeedRunRow(
            ProbeReferenceSeeder.SeederKey, "REFERENCE", "UG,KE", "Testing", 2, 0, 0,
            App.Clock.GetUtcNow(), App.Clock.GetUtcNow(), SystemActor.Id));
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

        TestSeeders(report).Select(s => (s.Key, s.Skipped)).ShouldBe([(ProbeReferenceSeeder.SeederKey, false), ("test.reference.second", true)]);
        TestSeeders(report)[0].Outcome.ShouldBe(new SeedOutcome(1, 0, 0));
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
        await ShouldBeUnlockedAsync(); // A failed run releases the seed lock.
    }

    [Fact]
    public async Task A_checksum_too_long_to_record_stops_the_run_before_the_seeder_writes()
    {
        App.Seeds.ProbeCodes = [.. Enumerable.Range(1, 40).Select(i => $"P{i:00}")]; // Checksum: 40 × 3 + 39 commas.

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Runner(App).RunAsync(new SeedRequest(SeedCategory.Reference), Ct));

        exception.Message.ShouldContain(ProbeReferenceSeeder.SeederKey);
        App.Seeds.Runs.ShouldBeEmpty();
        (await ProbeCodesAsync()).ShouldBeEmpty();
        (await SeedRunsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_demo_seeder_gets_no_owner_context()
    {
        App.Seeds.DemoAsksForOwnerContext = true;

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Runner(App).RunAsync(new SeedRequest(SeedCategory.Demo), Ct));

        exception.Message.ShouldContain("never as the owner");
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

    /// <summary>The report's entries for the test seeders, in run order.</summary>
    private static List<SeederResult> TestSeeders(SeedReport report) => [.. report.Seeders.Where(s => s.Key.StartsWith("test.", StringComparison.Ordinal))];

    private static SeedRunner Runner(WebApplicationFactory<Program> app) => app.Services.GetRequiredService<SeedRunner>();

    // The test app in another environment: same database, actor, clock and test seeders. No outbox processor, no
    // startup migration (Development's appsettings turn it on). Development's appsettings also name the developer's
    // local database: fail before anything runs unless the test container's connections won.
    private WebApplicationFactory<Program> InEnvironment(string environment, Dictionary<string, string?>? settings = null)
    {
        var overrides = new Dictionary<string, string?>(settings ?? [])
        {
            ["Outbox:Enabled"] = "false",
            [StartupMigration.SettingKey] = "false",

            // Staging and Production refuse to start without a real base domain (#35 R1).
            ["Platform:BaseDomain"] = "sports.test",

            // ...and without a mail server (S03-T1b); nothing sends here.
            ["Email:From"] = "no-reply@sports.test",
            ["Email:Smtp:Host"] = "smtp.sports.test",

            // ...and without the admin web's address for emailed links (S03-T2b).
            ["Identity:AdminAppUrl"] = "https://admin.sports.test",
        };
        var app = App.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(overrides)));

        var configuration = app.Services.GetRequiredService<IConfiguration>();
        foreach (var (name, role) in new[] { (ConnectionStringNames.Migrator, DatabaseRoles.Migrator), (ConnectionStringNames.App, DatabaseRoles.App) })
        {
            if (!overrides.ContainsKey($"ConnectionStrings:{name}"))
            {
                configuration.GetConnectionString(name).ShouldBe(Fixture.ConnectionString(role));
            }
        }

        return app;
    }

    private string FixtureHost => new NpgsqlConnectionStringBuilder(Fixture.ConnectionString(DatabaseRoles.Migrator)).Host!;

    private async Task<NpgsqlConnection> OpenMigratorAsync()
    {
        var connection = new NpgsqlConnection(Fixture.ConnectionString(DatabaseRoles.Migrator) + ";Pooling=false");
        await connection.OpenAsync(Ct);
        return connection;
    }

    private static async Task<bool> AdvisoryAsync(NpgsqlConnection connection, string function)
    {
        await using var command = new NpgsqlCommand($"SELECT {function}({SeedRunner.LockKey})", connection);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task ShouldBeUnlockedAsync()
    {
        await using var connection = await OpenMigratorAsync();
        (await AdvisoryAsync(connection, "pg_try_advisory_lock")).ShouldBeTrue();
        await AdvisoryAsync(connection, "pg_advisory_unlock");
    }

    private Task<List<string>> ProbeCodesAsync() => QueryAsync(
        DatabaseRoles.App, "SELECT code FROM conventions_test.reference_probes ORDER BY code", reader => reader.GetString(0));

    /// <summary>The test seeders' runs, oldest first.</summary>
    private Task<List<SeedRunRow>> SeedRunsAsync() => QueryAsync(
        DatabaseRoles.App,
        "SELECT seeder_key, category, checksum, environment, inserted, updated, deactivated, started_at, completed_at, created_by " +
        "FROM platform.seed_runs WHERE seeder_key LIKE 'test.%' ORDER BY id",
        reader => new SeedRunRow(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
            reader.GetInt32(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8), reader.GetGuid(9)));

    private sealed record SeedRunRow(
        string SeederKey, string Category, string Checksum, string Environment, int Inserted, int Updated, int Deactivated,
        DateTimeOffset StartedAt, DateTimeOffset CompletedAt, Guid CreatedBy);
}
