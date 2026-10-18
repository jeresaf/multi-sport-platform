using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Seed.Reference;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary>Platform's REFERENCE seeders on the test database, which the fixture has already seeded.</summary>
public sealed class ReferenceDataSeedTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _seeders =
        [CountrySeeder.SeederKey, CurrencySeeder.SeederKey, LanguageSeeder.SeederKey, TimeZoneSeeder.SeederKey];

    [Fact]
    public async Task Running_the_reference_seed_twice_changes_nothing()
    {
        var before = await SnapshotAsync();

        // seed_runs is empty after the reset, so every seeder runs against the data already there.
        var first = await RunAsync(App);
        await Fixture.ResetAsync(); // Forget the runs, keep the data: the seeders run once more.
        var second = await RunAsync(App);
        var third = await RunAsync(App);

        first.ShouldAllBe(s => !s.Skipped && s.Outcome == SeedOutcome.None);
        second.ShouldAllBe(s => !s.Skipped && s.Outcome == SeedOutcome.None);
        third.ShouldAllBe(s => s.Skipped);
        (await SnapshotAsync()).ShouldBe(before); // xmin included: no row was even rewritten.
    }

    [Fact]
    public async Task Empty_tables_get_every_entry_with_its_deterministic_ID_as_the_system_actor()
    {
        try
        {
            foreach (var table in PostgresFixture.ReferenceTables)
            {
                await ExecuteAsync(DatabaseRoles.Migrator, $"DELETE FROM platform.{table}");
            }

            var report = await RunAsync(App);

            var files = App.Services.GetRequiredService<ReferenceDataFiles>();
            report.Select(s => s.Outcome.Inserted).ShouldBe([
                files.Items<CountryItem>("countries.json").Count,
                files.Items<CurrencyItem>("currencies.json").Count,
                files.Items<LanguageItem>("languages.json").Count,
                files.Items<TimeZoneItem>("time-zones.json").Count,
            ]);
            (await QueryAsync(
                DatabaseRoles.App,
                "SELECT id, alpha3_code, numeric_code, name, is_active, created_by FROM platform.countries WHERE code = 'UG'",
                reader => (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetGuid(5))))
                .ShouldHaveSingleItem().ShouldBe((Country.IdFor("UG"), "UGA", "800", "Uganda", true, SystemActor.Id));
            (await QueryAsync(DatabaseRoles.App, "SELECT id, minor_units FROM platform.currencies WHERE code = 'UGX'",
                reader => (reader.GetGuid(0), reader.GetInt16(1))))
                .ShouldHaveSingleItem().ShouldBe((Currency.IdFor("UGX"), (short)0));
            (await QueryAsync(DatabaseRoles.App, "SELECT id, country_code FROM platform.time_zones WHERE code = 'Africa/Kampala'",
                reader => (reader.GetGuid(0), reader.GetString(1))))
                .ShouldHaveSingleItem().ShouldBe((IanaTimeZone.IdFor("Africa/Kampala"), "UG"));
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }
    }

    [Fact]
    public async Task A_code_dropped_from_the_data_is_deactivated_not_deleted_and_comes_back_when_listed_again()
    {
        try
        {
            await using var edited = WithCountries(countries =>
            {
                countries.Remove(countries.Single(c => (string?)c!["code"] == "KE"));
                countries.Single(c => (string?)c!["code"] == "UG")!["name"] = "Republic of Uganda";
            });

            var report = await RunAsync(edited);

            report.Single(s => s.Key == CountrySeeder.SeederKey).Outcome.ShouldBe(new SeedOutcome(0, 1, 1));
            report.Where(s => s.Key != CountrySeeder.SeederKey).ShouldAllBe(s => s.Outcome == SeedOutcome.None);
            (await CountriesAsync("KE", "UG")).ShouldBe([("KE", "Kenya", false), ("UG", "Republic of Uganda", true)]);
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }

        (await CountriesAsync("KE", "UG")).ShouldBe([("KE", "Kenya", true), ("UG", "Uganda", true)]);
    }

    [Fact]
    public async Task A_new_code_may_take_the_numeric_and_alpha3_codes_of_one_dropped_in_the_same_update()
    {
        try
        {
            // ISO retires KE and gives its numbers to a new entry: the old row must be deactivated before the insert.
            await using var edited = WithCountries(countries =>
            {
                countries.Remove(countries.Single(c => (string?)c!["code"] == "KE"));
                countries.Add(new JsonObject { ["code"] = "QZ", ["alpha3Code"] = "KEN", ["numericCode"] = "404", ["name"] = "Successor of Kenya" });
            });

            var report = await RunAsync(edited);

            report.Single(s => s.Key == CountrySeeder.SeederKey).Outcome.ShouldBe(new SeedOutcome(1, 0, 1));
            (await QueryAsync(
                DatabaseRoles.App,
                "SELECT code, alpha3_code, numeric_code, is_active FROM platform.countries WHERE numeric_code = '404' ORDER BY code",
                reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3))))
                .ShouldBe([("KE", "KEN", "404", false), ("QZ", "KEN", "404", true)]);
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }
    }

    private static async Task<List<SeederResult>> RunAsync(WebApplicationFactory<Program> app)
    {
        var report = await app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Reference), Ct);
        report.IsRefused.ShouldBeFalse();
        return [.. report.Seeders.Where(s => _seeders.Contains(s.Key)).OrderBy(s => Array.IndexOf(_seeders, s.Key))];
    }

    // The test app with an edited countries.json: same database, no outbox processor.
    private WebApplicationFactory<Program> WithCountries(Action<JsonArray> edit) => App.WithWebHostBuilder(builder => builder
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" }))
        .ConfigureTestServices(services => services.AddSingleton<ReferenceDataFiles>(new EditedFiles("countries.json", edit))));

    private Task<List<(string Code, string Name, bool IsActive)>> CountriesAsync(params string[] codes) => QueryAsync(
        DatabaseRoles.App,
        $"SELECT code, name, is_active FROM platform.countries WHERE code IN ({string.Join(", ", codes.Select(c => $"'{c}'"))}) ORDER BY code",
        reader => (reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));

    /// <summary>Every reference row as text, with its <c>xmin</c>: any write changes it.</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        var rows = new List<string>();
        foreach (var table in PostgresFixture.ReferenceTables)
        {
            var tableRows = await QueryAsync(
                DatabaseRoles.App, $"SELECT '{table}' || ' ' || xmin::text || ' ' || t::text FROM platform.{table} t ORDER BY code",
                reader => reader.GetString(0));
            tableRows.ShouldNotBeEmpty(table);
            rows.AddRange(tableRows);
        }

        return rows;
    }

    private sealed class EditedFiles(string file, Action<JsonArray> edit) : ReferenceDataFiles
    {
        public override byte[] Read(string name)
        {
            var bytes = base.Read(name);
            if (name != file)
            {
                return bytes;
            }

            var document = JsonNode.Parse(bytes)!;
            edit(document["items"]!.AsArray());
            return Encoding.UTF8.GetBytes(document.ToJsonString());
        }
    }
}
