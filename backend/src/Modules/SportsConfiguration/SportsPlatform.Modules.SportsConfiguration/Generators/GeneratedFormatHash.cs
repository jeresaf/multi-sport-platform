using System.Globalization;
using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Hashing;

namespace SportsPlatform.Modules.SportsConfiguration.Generators;

/// <summary>
/// The output hash of a generated format (S05-T3b plan): <see cref="CanonicalJson"/>'s hash of the inputs and the
/// structure, written field by field so a change to the contract's records can't change it silently. The seed is a
/// decimal string: JSON numbers are doubles and lose a 64-bit seed's low digits. A change to this form changes every
/// stored hash, so it needs a new scheme.
/// </summary>
public static class GeneratedFormatHash
{
    public static string Compute(
        string formatCode,
        int formatVersion,
        IReadOnlyDictionary<string, ParameterValue> parameters,
        ulong seed,
        IReadOnlyList<string?> entrants,
        IReadOnlyList<GeneratedStage> stages)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(entrants);
        ArgumentNullException.ThrowIfNull(stages);
        var content = new JsonObject
        {
            ["format"] = new JsonObject { ["code"] = formatCode, ["version"] = formatVersion },
            ["parameters"] = new JsonObject(parameters.Select(entry => KeyValuePair.Create(entry.Key, Value(entry.Value)))),
            ["seed"] = seed.ToString(CultureInfo.InvariantCulture),
            ["entrants"] = new JsonArray([.. entrants.Select(entrant => (JsonNode?)entrant)]),
            ["stages"] = new JsonArray([.. stages.Select(Stage)]),
        };
        return CanonicalJson.Hash(content);
    }

    private static JsonNode? Value(ParameterValue value) => value.Type switch
    {
        ParameterType.Number => value.Number,
        ParameterType.Flag => value.Flag,
        ParameterType.Choice => value.Choice,
        _ => throw Unmapped($"{nameof(ParameterType)}.{value.Type}"),
    };

    private static JsonObject Stage(GeneratedStage stage) => new JsonObject
    {
        ["code"] = stage.Code,
        ["type"] = StageTypeCode(stage.Type),
        ["rounds"] = new JsonArray([.. stage.Rounds.Select(Round)]),
    };

    private static JsonObject Round(GeneratedRound round) => new JsonObject
    {
        ["number"] = round.Number,
        ["code"] = round.Code,
        ["remainingEntrants"] = round.RemainingEntrants,
        ["pairings"] = new JsonArray([.. round.Pairings.Select(Pairing)]),
    };

    private static JsonObject Pairing(GeneratedPairing pairing) => new JsonObject
    {
        ["code"] = pairing.Code,
        ["home"] = Side(pairing.Home),
        ["away"] = Side(pairing.Away),
    };

    private static JsonObject Side(PairingSide side) => new JsonObject
    {
        ["kind"] = side.Kind switch
        {
            PairingSideKind.Entrant => "ENTRANT",
            PairingSideKind.WinnerOf => "WINNER_OF",
            PairingSideKind.LoserOf => "LOSER_OF",
            PairingSideKind.Bye => "BYE",
            _ => throw Unmapped($"{nameof(PairingSideKind)}.{side.Kind}"),
        },
        ["reference"] = side.Reference,
    };

    private static string StageTypeCode(StageType type) => type switch
    {
        StageType.League => "LEAGUE",
        StageType.Group => "GROUP",
        StageType.Knockout => "KNOCKOUT",
        StageType.Series => "SERIES",
        StageType.Single => "SINGLE",
        _ => throw Unmapped($"{nameof(StageType)}.{type}"),
    };

    // A new member needs its own code here: a catch-all would let two draws share a hash.
    // (Named by the caller: Type.Name is System.Reflection, which the purity rule refuses.)
    private static NotSupportedException Unmapped(string member) => new($"The generated format's hash has no form for {member}.");
}
