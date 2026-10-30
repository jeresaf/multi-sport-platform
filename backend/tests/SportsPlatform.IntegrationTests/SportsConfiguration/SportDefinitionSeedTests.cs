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
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Sports;
using SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary><c>config.configuration.sports</c> on the test database, which the fixture has already seeded (S05 D4).</summary>
public sealed class SportDefinitionSeedTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _football = "football.yaml";

    [Fact]
    public async Task Football_version_1_is_stored_with_its_canonical_content_and_hash_as_the_system_actor()
    {
        var expected = YamlJson.Parse(new SportDefinitionFiles().Read(_football));

        var row = (await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, variant, sport, version, name, scoring_family, shape, status, content::text, content_hash, created_by " +
            "FROM config.sport_definitions",
            reader => (Id: reader.GetGuid(0), Variant: reader.GetString(1), Sport: reader.GetString(2), Version: reader.GetInt32(3),
                Name: reader.GetString(4), Family: reader.GetString(5), Shape: reader.GetString(6), Status: reader.GetString(7),
                Content: reader.GetString(8), Hash: reader.GetString(9), CreatedBy: reader.GetGuid(10)))).ShouldHaveSingleItem();

        (row.Variant, row.Sport, row.Version, row.Name, row.Family, row.Shape, row.Status)
            .ShouldBe(("FOOTBALL_ASSOCIATION", "FOOTBALL", 1, "Football", "ADDITIVE", "HEAD_TO_HEAD", "PUBLISHED"));
        row.Id.ShouldBe(PublishedSportDefinition.IdFor("FOOTBALL_ASSOCIATION", 1));
        row.CreatedBy.ShouldBe(SystemActor.Id);
        // jsonb keeps the tree, not the text: canonicalizing what comes back gives the stored hash.
        row.Hash.ShouldBe(CanonicalJson.Hash(expected));
        CanonicalJson.Hash(JsonNode.Parse(row.Content)).ShouldBe(row.Hash);
    }

    [Fact]
    public async Task Running_the_sports_seed_again_changes_nothing()
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
    public async Task Changing_a_published_definition_without_a_new_version_fails_and_writes_nothing()
    {
        var before = await SnapshotAsync();
        await using var edited = WithFootball(yaml => yaml.Replace("\nname: Football\n", "\nname: Football (edited)\n", StringComparison.Ordinal));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldStartWith("SPORT_DEFINITION_IMMUTABLE: football.yaml: FOOTBALL_ASSOCIATION@1 is published as sha256:");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task A_new_version_is_added_next_to_the_published_one()
    {
        try
        {
            await using var edited = WithFootball(yaml => yaml
                .Replace("\nversion: 1\n", "\nversion: 2\n", StringComparison.Ordinal)
                .Replace("\nname: Football\n", "\nname: Association football\n", StringComparison.Ordinal));

            (await RunAsync(edited)).Outcome.ShouldBe(new SeedOutcome(1, 0, 0));

            (await QueryAsync(DatabaseRoles.App, "SELECT version, name FROM config.sport_definitions ORDER BY version",
                reader => (reader.GetInt32(0), reader.GetString(1))))
                .ShouldBe([(1, "Football"), (2, "Association football")]);
            await using var scope = edited.Services.CreateAsyncScope();
            var definitions = scope.ServiceProvider.GetRequiredService<ISportDefinitions>();
            (await definitions.ListAsync(Ct)).ShouldHaveSingleItem().ShouldBe(
                new SportVariant("FOOTBALL_ASSOCIATION", "FOOTBALL", "Association football", "ADDITIVE", ContestShape.HeadToHead, 2));
            (await definitions.GetAsync("FOOTBALL_ASSOCIATION", null, Ct)).ShouldNotBeNull().Version.ShouldBe(2);
            (await definitions.GetAsync("FOOTBALL_ASSOCIATION", 1, Ct)).ShouldNotBeNull().Name.ShouldBe("Football");
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "DELETE FROM config.sport_definitions WHERE version > 1");
            await Fixture.RestoreSeedDataAsync();
        }
    }

    [Fact]
    public async Task An_invalid_definition_fails_the_seed()
    {
        var before = await SnapshotAsync();
        await using var edited = WithFootball(yaml => yaml.Replace("role: SCORER,", "role: SCORERR,", StringComparison.Ordinal), startupCheck: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldContain("football.yaml: $.event_types[0].actors[0].role: unknown actor role \"SCORERR\"");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Two_files_declaring_the_same_version_fail_the_seed()
    {
        var before = await SnapshotAsync();
        await using var doubled = App.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
            .ConfigureTestServices(services => services.AddSingleton<SportDefinitionFiles>(new DoubledFiles())));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(doubled));

        exception.Message.ShouldBe("soccer.yaml and football.yaml both declare FOOTBALL_ASSOCIATION@1.");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public void The_checksum_changes_with_the_content_only()
    {
        var shipped = new SportDefinitionSeeder(new SportDefinitionFiles(), TimeProvider.System).Checksum;

        new SportDefinitionSeeder(new EditedFiles(yaml => yaml + "# a trailing comment\n"), TimeProvider.System).Checksum.ShouldBe(shipped);
        new SportDefinitionSeeder(new EditedFiles(yaml => yaml.Replace("\nname: Football\n", "\nname: Soccer\n", StringComparison.Ordinal)), TimeProvider.System)
            .Checksum.ShouldNotBe(shipped);
    }

    private static async Task<SeederResult> RunAsync(WebApplicationFactory<Program> app)
    {
        var report = await app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Configuration), Ct);
        report.IsRefused.ShouldBeFalse();
        return report.Seeders.Single(s => s.Key == SportDefinitionSeeder.SeederKey);
    }

    // The test app serving an edited football file: same database, no outbox processor. Without the startup check, an
    // invalid file reaches the seeder (the check would stop the Host first).
    private WebApplicationFactory<Program> WithFootball(Func<string, string> edit, bool startupCheck = true) => App.WithWebHostBuilder(builder => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
        .ConfigureTestServices(services =>
        {
            services.AddSingleton<SportDefinitionFiles>(new EditedFiles(edit));
            if (!startupCheck)
            {
                services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Name == "SportDefinitionStartupCheck"));
            }
        }));

    /// <summary>Every definition row as text, with its <c>xmin</c>: any write changes it.</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        var rows = await QueryAsync(
            DatabaseRoles.App,
            "SELECT xmin::text || ' ' || t::text FROM config.sport_definitions t ORDER BY id",
            reader => reader.GetString(0));
        rows.ShouldNotBeEmpty();
        return rows;
    }

    private sealed class DoubledFiles : SportDefinitionFiles
    {
        public override IReadOnlyList<string> Names => [.. base.Names, "soccer.yaml"];

        public override string Read(string file) => base.Read(file == "soccer.yaml" ? _football : file);
    }

    private sealed class EditedFiles(Func<string, string> edit) : SportDefinitionFiles
    {
        public override string Read(string file) => file == _football ? edit(base.Read(file)) : base.Read(file);
    }
}
