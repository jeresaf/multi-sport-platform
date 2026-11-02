using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Generators;

namespace SportsPlatform.UnitTests.SportsConfiguration.Generators;

internal static class GeneratorTestData
{
    private static readonly FormatDefinitionFiles _files = new();

    public static FormatGenerators Generator { get; } = new();

    public static FormatDefinition RoundRobin { get; } = Read("round-robin.yaml");

    public static FormatDefinition Knockout { get; } = Read("knockout.yaml");

    public static FormatDefinition SingleContest { get; } = Read("single-contest.yaml");

    // KMSA's football clubs (docs/product/pilot-customer.md), for draws a reader follows by name.
    public static IReadOnlyList<string> Clubs { get; } =
    [
        "BUKOTO_COMETS", "KANSANGA_HARRIERS", "NAKAWA_NOMADS", "KIREKA_KINGFISHERS", "BUNGA_BREAKERS", "SEETA_SPARROWS",
        "KYEBANDO_RAVENS", "MUTUNDWE_METEORS",
    ];

    /// <summary><paramref name="count"/> keys standing for entrants; the generators never read their meaning.</summary>
    public static IReadOnlyList<string> Entrants(int count) =>
        [.. Enumerable.Range(1, count).Select(i => $"ENTRANT_{i:D3}")];

    public static FormatGenerationInput Input(IReadOnlyList<string?> entrants, ulong seed = 20261102, params (string Code, ParameterValue Value)[] parameters) =>
        new(entrants, parameters.ToDictionary(p => p.Code, p => p.Value, StringComparer.Ordinal), seed);

    public static GeneratedFormat Generate(FormatDefinition format, FormatGenerationInput input)
    {
        var result = Generator.Generate(format, input);
        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        return result.Value;
    }

    public static IEnumerable<GeneratedPairing> Contests(this GeneratedFormat generated) =>
        generated.Stages.SelectMany(stage => stage.Rounds).SelectMany(round => round.Pairings).Where(pairing => !pairing.IsBye);

    private static FormatDefinition Read(string file) => FormatDefinitionContent.Read(YamlJson.Parse(_files.Read(file))!);
}
