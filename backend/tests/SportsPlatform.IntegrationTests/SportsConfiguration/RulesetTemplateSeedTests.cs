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
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Hashing;
using SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary><c>config.configuration.rulesets</c> on the test database, which the fixture has already seeded (S05 D4, D11).</summary>
public sealed class RulesetTemplateSeedTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _league = "football-league.yaml";

    private const string _winBounds = "standings.points.win: { type: int, label: Points for a win, min: 1, max: 5 }";

    [Fact]
    public async Task The_templates_are_stored_with_their_references_canonical_content_and_hash_as_the_system_actor()
    {
        var files = new RulesetTemplateFiles();

        var rows = await QueryAsync(
            DatabaseRoles.App,
            "SELECT id, code, version, name, sport_variant, sport_version, formats, status, content::text, content_hash, created_by " +
            "FROM config.ruleset_templates ORDER BY code",
            reader => (Id: reader.GetGuid(0), Code: reader.GetString(1), Version: reader.GetInt32(2), Name: reader.GetString(3),
                Sport: (reader.GetString(4), reader.GetInt32(5)), Formats: reader.GetFieldValue<string[]>(6), Status: reader.GetString(7),
                Content: reader.GetString(8), Hash: reader.GetString(9), CreatedBy: reader.GetGuid(10)));

        rows.Select(row => (row.Code, row.Version, row.Name, row.Sport, row.Status)).ShouldBe(
        [
            ("FOOTBALL_CUP", 1, "Football cup", ("FOOTBALL_ASSOCIATION", 1), "PUBLISHED"),
            ("FOOTBALL_LEAGUE", 1, "Football league", ("FOOTBALL_ASSOCIATION", 1), "PUBLISHED"),
        ]);
        rows[0].Formats.ShouldBe(["KNOCKOUT", "SINGLE_CONTEST"]);
        rows[1].Formats.ShouldBe(["ROUND_ROBIN", "SINGLE_CONTEST"]);
        foreach (var (row, file) in rows.Zip(new[] { "football-cup.yaml", _league }))
        {
            row.Id.ShouldBe(PublishedRulesetTemplate.IdFor(row.Code, 1));
            row.CreatedBy.ShouldBe(SystemActor.Id);
            row.Hash.ShouldBe(CanonicalJson.Hash(YamlJson.Parse(files.Read(file))));
            // jsonb keeps the tree, not the text: canonicalizing what comes back gives the stored hash.
            CanonicalJson.Hash(JsonNode.Parse(row.Content)).ShouldBe(row.Hash);
        }
    }

    [Fact]
    public void Templates_are_seeded_after_sports_and_formats()
    {
        using var scope = App.Services.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<ISeeder>().Where(seeder => seeder.Key.StartsWith("config.", StringComparison.Ordinal)).ToList();

        var order = seeders.Single(seeder => seeder.Key == RulesetTemplateSeeder.SeederKey).Order;
        order.ShouldBeGreaterThan(seeders.Single(seeder => seeder.Key == SportDefinitionSeeder.SeederKey).Order);
        order.ShouldBeGreaterThan(seeders.Single(seeder => seeder.Key == FormatDefinitionSeeder.SeederKey).Order);
    }

    [Fact]
    public async Task Running_the_templates_seed_again_changes_nothing()
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
    public async Task Changing_a_published_template_without_a_new_version_fails_and_writes_nothing()
    {
        var before = await SnapshotAsync();
        await using var edited = WithLeague(yaml => yaml.Replace("max: 5 }", "max: 6 }", StringComparison.Ordinal));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldStartWith($"{RulesetTemplateSeeder.ImmutableCode}: football-league.yaml: FOOTBALL_LEAGUE@1 is published as sha256:");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task A_new_version_is_added_next_to_the_published_one()
    {
        try
        {
            await using var edited = WithLeague(yaml => yaml
                .Replace("\nversion: 1\n", "\nversion: 2\n", StringComparison.Ordinal)
                .Replace(_winBounds, _winBounds.Replace("max: 5", "max: 6", StringComparison.Ordinal), StringComparison.Ordinal));

            (await RunAsync(edited)).Outcome.ShouldBe(new SeedOutcome(1, 0, 0));

            (await QueryAsync(DatabaseRoles.App, "SELECT version FROM config.ruleset_templates WHERE code = 'FOOTBALL_LEAGUE' ORDER BY version",
                reader => reader.GetInt32(0))).ShouldBe([1, 2]);
            await using var scope = edited.Services.CreateAsyncScope();
            var templates = scope.ServiceProvider.GetRequiredService<IRulesetTemplates>();
            (await templates.ListAsync(Ct)).Single(template => template.Code == "FOOTBALL_LEAGUE").Version.ShouldBe(2);
            (await templates.GetAsync("FOOTBALL_LEAGUE", null, Ct)).ShouldNotBeNull().Editable["standings.points.win"].Max.ShouldBe(6);
            (await templates.GetAsync("FOOTBALL_LEAGUE", 1, Ct)).ShouldNotBeNull().Editable["standings.points.win"].Max.ShouldBe(5);
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "DELETE FROM config.ruleset_templates WHERE version > 1");
            await Fixture.RestoreSeedDataAsync();
        }
    }

    // The CLI's seed runs no startup check: the seeder reports the validator's problems itself, before anything is written.
    [Fact]
    public async Task An_invalid_template_fails_the_seed_with_its_problems()
    {
        var before = await SnapshotAsync();
        await using var edited = WithLeague(yaml => yaml.Replace("sport: { variant: FOOTBALL_ASSOCIATION, version: 1 }", "sport: { variant: FOOTBALL_ASSOCIATION, version: 9 }", StringComparison.Ordinal), startupCheck: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(edited));

        exception.Message.ShouldStartWith("Ruleset templates are invalid:");
        exception.Message.ShouldContain("football-league.yaml: $.sport: no sport definition FOOTBALL_ASSOCIATION@9");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Two_files_declaring_the_same_version_fail_the_seed()
    {
        var before = await SnapshotAsync();
        await using var doubled = App.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
            .ConfigureTestServices(services => services.AddSingleton<RulesetTemplateFiles>(new DoubledFiles())));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(doubled));

        exception.Message.ShouldStartWith($"{RulesetTemplateSeeder.DuplicateVersionCode}: football-league.yaml and a-league.yaml both declare FOOTBALL_LEAGUE@1");
        (await SnapshotAsync()).ShouldBe(before);
    }

    private static async Task<SeederResult> RunAsync(WebApplicationFactory<Program> app)
    {
        var report = await app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Configuration), Ct);
        report.IsRefused.ShouldBeFalse();
        return report.Seeders.Single(s => s.Key == RulesetTemplateSeeder.SeederKey);
    }

    // The test app serving an edited league: same database, no outbox processor. Without the startup check, an invalid
    // file reaches the seeder (the check would stop the Host first).
    private WebApplicationFactory<Program> WithLeague(Func<string, string> edit, bool startupCheck = true) => App.WithWebHostBuilder(builder => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
        .ConfigureTestServices(services =>
        {
            services.AddSingleton<RulesetTemplateFiles>(new EditedFiles(edit));
            if (!startupCheck)
            {
                services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Name == "DefinitionStartupCheck"));
            }
        }));

    /// <summary>Every template row as text, with its <c>xmin</c>: any write changes it.</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        var rows = await QueryAsync(
            DatabaseRoles.App,
            "SELECT xmin::text || ' ' || t::text FROM config.ruleset_templates t ORDER BY id",
            reader => reader.GetString(0));
        rows.ShouldNotBeEmpty();
        return rows;
    }

    // a-league.yaml sorts before football-league.yaml, so the league file is the second to declare FOOTBALL_LEAGUE@1.
    private sealed class DoubledFiles : RulesetTemplateFiles
    {
        public override IReadOnlyList<string> Names => ["a-league.yaml", .. base.Names];

        public override string Read(string file) => base.Read(file == "a-league.yaml" ? _league : file);
    }

    private sealed class EditedFiles(Func<string, string> edit) : RulesetTemplateFiles
    {
        public override string Read(string file) => file == _league ? edit(base.Read(file)) : base.Read(file);
    }
}
