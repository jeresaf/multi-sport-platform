using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary><see cref="ISportDefinitions"/> over the seeded <c>config.sport_definitions</c> (S05 D5).</summary>
public sealed class SportDefinitionsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _variant = "FOOTBALL_ASSOCIATION";

    [Fact]
    public async Task The_list_has_each_published_variant_at_its_latest_version()
    {
        (await ListAsync()).ShouldBe([new SportVariant(_variant, "FOOTBALL", "Football", "ADDITIVE", ContestShape.HeadToHead, 1)]);
    }

    [Fact]
    public async Task A_definition_is_resolved_with_the_generic_event_types_and_its_stored_hash()
    {
        var definition = (await GetAsync(_variant, null)).ShouldNotBeNull();

        definition.Version.ShouldBe(1);
        definition.ContentHash.ShouldBe(CanonicalJson.Hash(YamlJson.Parse(new SportDefinitionFiles().Read("football.yaml"))));
        definition.PublishedAt.ShouldNotBe(default);
        definition.Terminology.ShouldBe(new SportTerminology("Match", "Matches", "Half", "Team", "Player", "Goal"));
        definition.Periods.Select(period => period.ShortLabel).ShouldBe(["1H", "HT", "2H", "BET", "ET1", "ETB", "ET2", "BP", "PEN"]);
        definition.EventTypes.Select(type => type.Code).ShouldContain("GOAL");
        definition.GenericEventTypes.ShouldBe(GenericEventTypes.All);
        definition.GenericEventTypes.Select(type => type.Code).ShouldContain("EVENT_REVERSED");
    }

    [Fact]
    public async Task A_version_is_found_by_its_number_and_an_unknown_one_is_not()
    {
        (await GetAsync(_variant, 1)).ShouldNotBeNull().Version.ShouldBe(1);
        (await GetAsync(_variant, 2)).ShouldBeNull();
        (await GetAsync("CURLING", null)).ShouldBeNull();
        // Codes match exactly.
        (await GetAsync(_variant.ToLowerInvariant(), null)).ShouldBeNull();
    }

    [Fact]
    public async Task Definitions_are_read_once_and_kept_until_restart()
    {
        (await ListAsync()).ShouldNotBeEmpty();
        try
        {
            // Not possible for the runtime roles; the owner stands in for "the table changed under a running app".
            await ExecuteAsync(DatabaseRoles.Migrator, "UPDATE config.sport_definitions SET name = 'Renamed'");

            (await ListAsync()).ShouldHaveSingleItem().Name.ShouldBe("Football");
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "UPDATE config.sport_definitions SET name = 'Football'");
        }
    }

    private async Task<IReadOnlyList<SportVariant>> ListAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISportDefinitions>().ListAsync(Ct);
    }

    private async Task<SportDefinition?> GetAsync(string variant, int? version)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISportDefinitions>().GetAsync(variant, version, Ct);
    }
}
