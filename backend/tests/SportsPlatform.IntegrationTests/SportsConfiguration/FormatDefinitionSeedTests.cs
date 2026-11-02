using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Formats;
using SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary><c>config.configuration.formats</c> on the test database, which the fixture has already seeded (S05 D4, D10).</summary>
public sealed class FormatDefinitionSeedTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _roundRobin = "round-robin.yaml";

    [Fact]
    public async Task The_formats_are_stored_with_their_canonical_content_and_hash_as_the_system_actor()
    {
        var files = new FormatDefinitionFiles();

        var rows = await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, code, version, name, status, content::text, content_hash, created_by FROM config.format_definitions ORDER BY code",
            reader => (Id: reader.GetGuid(0), Code: reader.GetString(1), Version: reader.GetInt32(2), Name: reader.GetString(3),
                Status: reader.GetString(4), Content: reader.GetString(5), Hash: reader.GetString(6), CreatedBy: reader.GetGuid(7)));

        rows.Select(row => (row.Code, row.Version, row.Name, row.Status)).ShouldBe(
        [
            ("KNOCKOUT", 1, "Knockout", "PUBLISHED"),
            ("ROUND_ROBIN", 1, "League (round robin)", "PUBLISHED"),
            ("SINGLE_CONTEST", 1, "Single contest", "PUBLISHED"),
        ]);
        foreach (var (row, file) in rows.Zip(new[] { "knockout.yaml", _roundRobin, "single-contest.yaml" }))
        {
            row.Id.ShouldBe(PublishedFormatDefinition.IdFor(row.Code, 1));
            row.CreatedBy.ShouldBe(SystemActor.Id);
            row.Hash.ShouldBe(CanonicalJson.Hash(YamlJson.Parse(files.Read(file))));
            // jsonb keeps the tree, not the text: canonicalizing what comes back gives the stored hash.
            CanonicalJson.Hash(JsonNode.Parse(row.Content)).ShouldBe(row.Hash);
        }
    }

    [Fact]
    public void Formats_are_seeded_after_sports()
    {
        using var scope = App.Services.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<ISeeder>().Where(seeder => seeder.Key.StartsWith("config.", StringComparison.Ordinal)).ToList();

        seeders.Single(seeder => seeder.Key == FormatDefinitionSeeder.SeederKey).Order
            .ShouldBeGreaterThan(seeders.Single(seeder => seeder.Key == SportDefinitionSeeder.SeederKey).Order);
    }

    [Fact]
    public async Task Running_the_formats_seed_again_changes_nothing()
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
    public async Task Changing_a_published_format_without_a_new_version_fails_and_writes_nothing()
    {
        var before = await SnapshotAsync();
        await using var edited = WithRoundRobin(yaml => yaml.Replace("    max: 4\n", "    max: 3\n", StringComparison.Ordinal));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldStartWith("FORMAT_DEFINITION_IMMUTABLE: round-robin.yaml: ROUND_ROBIN@1 is published as sha256:");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task A_new_version_is_added_next_to_the_published_one()
    {
        try
        {
            await using var edited = WithRoundRobin(yaml => yaml
                .Replace("\nversion: 1\n", "\nversion: 2\n", StringComparison.Ordinal)
                .Replace("    max: 4\n", "    max: 3\n", StringComparison.Ordinal));

            (await RunAsync(edited)).Outcome.ShouldBe(new SeedOutcome(1, 0, 0));

            (await QueryAsync(DatabaseRoles.App, "SELECT version FROM config.format_definitions WHERE code = 'ROUND_ROBIN' ORDER BY version",
                reader => reader.GetInt32(0))).ShouldBe([1, 2]);
            await using var scope = edited.Services.CreateAsyncScope();
            var formats = scope.ServiceProvider.GetRequiredService<IFormatDefinitions>();
            (await formats.ListAsync(Ct)).Single(format => format.Code == "ROUND_ROBIN").Version.ShouldBe(2);
            (await formats.GetAsync("ROUND_ROBIN", null, Ct)).ShouldNotBeNull().Parameters[FormatParameters.Cycles].Max.ShouldBe(3);
            (await formats.GetAsync("ROUND_ROBIN", 1, Ct)).ShouldNotBeNull().Parameters[FormatParameters.Cycles].Max.ShouldBe(4);
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "DELETE FROM config.format_definitions WHERE version > 1");
            await Fixture.RestoreSeedDataAsync();
        }
    }

    // The CLI's seed runs no startup check: the seeder reports the validator's problems itself, before anything is written.
    [Fact]
    public async Task An_invalid_format_fails_the_seed_with_its_problems()
    {
        var before = await SnapshotAsync();
        await using var edited = WithRoundRobin(yaml => yaml.Replace("    default: 1\n", "    default: 9\n", StringComparison.Ordinal), startupCheck: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldStartWith("Format definitions are invalid:");
        exception.Message.ShouldContain("round-robin.yaml: $.parameters.cycles.default: 9 is outside 1..4");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Two_files_declaring_the_same_version_fail_the_seed()
    {
        var before = await SnapshotAsync();
        await using var doubled = App.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
            .ConfigureTestServices(services => services.AddSingleton<FormatDefinitionFiles>(new DoubledFiles())));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(doubled));

        exception.Message.ShouldStartWith($"{FormatDefinitionSeeder.DuplicateVersionCode}: round-robin.yaml and league.yaml both declare ROUND_ROBIN@1");
        (await SnapshotAsync()).ShouldBe(before);
    }

    private static async Task<SeederResult> RunAsync(WebApplicationFactory<Program> app)
    {
        var report = await app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Configuration), Ct);
        report.IsRefused.ShouldBeFalse();
        return report.Seeders.Single(s => s.Key == FormatDefinitionSeeder.SeederKey);
    }

    // The test app serving an edited round robin: same database, no outbox processor. Without the startup check, an
    // invalid file reaches the seeder (the check would stop the Host first).
    private WebApplicationFactory<Program> WithRoundRobin(Func<string, string> edit, bool startupCheck = true) => App.WithWebHostBuilder(builder => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
        .ConfigureTestServices(services =>
        {
            services.AddSingleton<FormatDefinitionFiles>(new EditedFiles(edit));
            if (!startupCheck)
            {
                services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Name == "DefinitionStartupCheck"));
            }
        }));

    /// <summary>Every format row as text, with its <c>xmin</c>: any write changes it.</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        var rows = await QueryAsync(
            DatabaseRoles.App,
            "SELECT xmin::text || ' ' || t::text FROM config.format_definitions t ORDER BY id",
            reader => reader.GetString(0));
        rows.ShouldNotBeEmpty();
        return rows;
    }

    // league.yaml sorts before round-robin.yaml, so the round robin is the second to declare ROUND_ROBIN@1.
    private sealed class DoubledFiles : FormatDefinitionFiles
    {
        public override IReadOnlyList<string> Names => [.. base.Names.Where(name => name != _roundRobin), "league.yaml", _roundRobin];

        public override string Read(string file) => base.Read(file == "league.yaml" ? _roundRobin : file);
    }

    private sealed class EditedFiles(Func<string, string> edit) : FormatDefinitionFiles
    {
        public override string Read(string file) => file == _roundRobin ? edit(base.Read(file)) : base.Read(file);
    }
}
