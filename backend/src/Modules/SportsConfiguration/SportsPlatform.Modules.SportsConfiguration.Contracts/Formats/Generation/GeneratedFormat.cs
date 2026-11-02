using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;

/// <summary>
/// A generated structure with its inputs: the format's code and version, every parameter (defaults filled in), the
/// seed and the entrants as given. <see cref="OutputHash"/> is <c>sha256:</c> and the hex SHA-256 of all of it in RFC
/// 8785 canonical JSON, so it identifies the whole draw (S05-T3b plan).
/// </summary>
public sealed record GeneratedFormat(
    string FormatCode,
    int FormatVersion,
    IReadOnlyDictionary<string, ParameterValue> Parameters,
    ulong Seed,
    IReadOnlyList<string?> Entrants,
    IReadOnlyList<GeneratedStage> Stages,
    string OutputHash);

/// <summary>A stage of the format, with its rounds in order.</summary>
public sealed record GeneratedStage(string Code, StageType Type, IReadOnlyList<GeneratedRound> Rounds);

/// <summary>
/// A round: its number (from 1, in playing order), its code and its pairings. A league's rounds are <c>ROUND_&lt;n&gt;</c>;
/// a knockout's are named by <see cref="BracketPlaces"/>, the bracket's places left with byes counted (<c>FINAL</c>,
/// <c>SEMI_FINALS</c>, <c>QUARTER_FINALS</c>, <c>ROUND_OF_&lt;n&gt;</c>, see <see cref="RoundCodes"/>): 5 entrants
/// start in <c>QUARTER_FINALS</c> with 8 places. The third-place contest is a round of its own (<c>THIRD_PLACE</c>,
/// no places) just before the final; a league round has no places either.
/// </summary>
public sealed record GeneratedRound(int Number, string Code, int? BracketPlaces, IReadOnlyList<GeneratedPairing> Pairings);

/// <summary>
/// A pairing, coded <c>&lt;round code&gt;_&lt;n&gt;</c> (<c>QUARTER_FINALS_1</c>, <c>ROUND_3_2</c>). A bye pairing has the
/// entrant at <see cref="Home"/> and <see cref="PairingSide.Bye"/> away; it makes no contest.
/// </summary>
public sealed record GeneratedPairing(string Code, PairingSide Home, PairingSide Away)
{
    public bool IsBye => Home.Kind == PairingSideKind.Bye || Away.Kind == PairingSideKind.Bye;
}

/// <summary>
/// One side of a pairing: an entrant, a slot filled later by the winner or the loser of an earlier pairing (its
/// <see cref="Reference"/> is that pairing's code), or a bye.
/// </summary>
public sealed record PairingSide(PairingSideKind Kind, string? Reference)
{
    public static PairingSide Bye { get; } = new(PairingSideKind.Bye, null);

    public static PairingSide Entrant(string key) => new(PairingSideKind.Entrant, key);

    public static PairingSide WinnerOf(string pairingCode) => new(PairingSideKind.WinnerOf, pairingCode);

    public static PairingSide LoserOf(string pairingCode) => new(PairingSideKind.LoserOf, pairingCode);

    public override string ToString() => Reference is null ? Kind.ToString() : $"{Kind}({Reference})";
}

public enum PairingSideKind
{
    Entrant,
    WinnerOf,
    LoserOf,
    Bye,
}

/// <summary>The knockout round codes; a league round is <c>ROUND_&lt;n&gt;</c>.</summary>
public static class RoundCodes
{
    public const string Final = "FINAL";

    public const string SemiFinals = "SEMI_FINALS";

    public const string QuarterFinals = "QUARTER_FINALS";

    public const string ThirdPlace = "THIRD_PLACE";

    /// <summary>The knockout round with <paramref name="bracketPlaces"/> places left in the bracket, byes counted.</summary>
    public static string ForBracketPlaces(int bracketPlaces) => bracketPlaces switch
    {
        2 => Final,
        4 => SemiFinals,
        8 => QuarterFinals,
        _ => $"ROUND_OF_{bracketPlaces}",
    };

    /// <summary>A league's round <paramref name="number"/>.</summary>
    public static string Round(int number) => $"ROUND_{number}";
}
