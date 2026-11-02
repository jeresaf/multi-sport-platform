using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

/// <summary>
/// What each fixture generator builds and reads (docs/domain/formats.md): the stage types it fills, the contest shapes
/// it pairs, how many participants it takes and its parameters. A format's stage names a generator from this list, and
/// declares exactly the parameters its generators read (FormatDefinitionRules); the generators (S05-T3b) read them.
/// </summary>
public static class FormatGeneratorKinds
{
    public const string SingleContest = "SINGLE_CONTEST";

    public const string RoundRobin = "ROUND_ROBIN";

    public const string Knockout = "KNOCKOUT";

    public static IReadOnlyDictionary<string, FormatGeneratorKind> Known { get; } = new Dictionary<string, FormatGeneratorKind>(StringComparer.Ordinal)
    {
        [SingleContest] = new([StageType.Single], [ContestShape.HeadToHead], MinParticipants: 2, MaxParticipants: 2, new Dictionary<string, GeneratorParameter>()),
        [RoundRobin] = new([StageType.League, StageType.Group], [ContestShape.HeadToHead], MinParticipants: 2, MaxParticipants: null, new Dictionary<string, GeneratorParameter>
        {
            [FormatParameters.Cycles] = new(ParameterType.Number, []),
            [FormatParameters.HomeAwayBalance] = new(ParameterType.Flag, []),
            [FormatParameters.Seeding] = new(ParameterType.Choice, Seedings.All),
        }),
        [Knockout] = new([StageType.Knockout], [ContestShape.HeadToHead], MinParticipants: 2, MaxParticipants: null, new Dictionary<string, GeneratorParameter>
        {
            [FormatParameters.ThirdPlaceContest] = new(ParameterType.Flag, []),
            [FormatParameters.Seeding] = new(ParameterType.Choice, Seedings.All),
        }),
    };
}

/// <summary>
/// A generator: the stage types it fills, the shapes it pairs, the participants it takes (<c>null</c>: no upper bound)
/// and the parameters it reads.
/// </summary>
public sealed record FormatGeneratorKind(
    IReadOnlyList<StageType> StageTypes,
    IReadOnlyList<ContestShape> Shapes,
    int MinParticipants,
    int? MaxParticipants,
    IReadOnlyDictionary<string, GeneratorParameter> Parameters);

/// <summary>A parameter a generator reads: its type, and for a choice the values it understands.</summary>
public sealed record GeneratorParameter(ParameterType Type, IReadOnlyList<string> Values);

/// <summary>The generators' parameter codes, as the format files write them.</summary>
public static class FormatParameters
{
    public const string Cycles = "cycles";

    public const string HomeAwayBalance = "home_away_balance";

    public const string Seeding = "seeding";

    public const string ThirdPlaceContest = "third_place_contest";
}

/// <summary>
/// The <c>seeding</c> values: <c>SEEDED</c> keeps the order given as seeds, <c>RANDOM</c> draws it with a stored seed,
/// <c>MANUAL</c> gives the placement (for a knockout, the bracket lines with their byes).
/// </summary>
public static class Seedings
{
    public const string Seeded = "SEEDED";

    public const string Random = "RANDOM";

    public const string Manual = "MANUAL";

    public static IReadOnlyList<string> All { get; } = [Seeded, Random, Manual];
}
