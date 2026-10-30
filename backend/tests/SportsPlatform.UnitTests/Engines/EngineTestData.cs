using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.UnitTests.SportsConfiguration;

namespace SportsPlatform.UnitTests.Engines;

/// <summary>Definitions and timeline events for the engine tests.</summary>
internal static class EngineTestData
{
    public static readonly Guid Home = Guid.Parse("0190a000-0000-7000-8000-000000000001");

    public static readonly Guid Away = Guid.Parse("0190a000-0000-7000-8000-000000000002");

    public static SportDefinition Football { get; } = Read(new SportDefinitionFiles().Read("football.yaml"));

    // Not seeded until S15, but its sin bin and temporary replacements exercise effects football doesn't have.
    public static SportDefinition RugbyUnion15 { get; } = Read(File.ReadAllText(Path.Combine(ReferenceSportFilesTests.RepositoryRoot(), "docs", "domain", "sports", "rugby-union-15.yaml")));

    public static ContestContext Context(SportDefinition definition, params ContestLineup[] lineups) =>
        new(definition, ContestRules.None, [new ContestSide("HOME", Home), new ContestSide("AWAY", Away)], CoverageLevel.Standard)
        {
            Lineups = lineups,
        };

    public static TimelineEvent Event(long sequence, string type, Guid? participant = null) =>
        new(Id(sequence), sequence, type, TimelineSource.Live) { ParticipantId = participant };

    public static TimelineEvent Start(long sequence, string period, int occurrence = 1) =>
        Event(sequence, GenericEventCodes.PeriodStart) with { PeriodCode = period, PeriodOccurrence = occurrence };

    public static Guid Id(long sequence) => Guid.Parse($"0190b000-0000-7000-8000-{sequence:D12}");

    public static Guid Person(int number) => Guid.Parse($"0190c000-0000-7000-8000-{number:D12}");

    private static SportDefinition Read(string yaml) =>
        SportDefinitionContent.Read(YamlJson.Parse(yaml)!) with { GenericEventTypes = GenericEventTypes.All };
}
