using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.Engines;

/// <summary>
/// An engine scenario file (S05 D9, sport-engine.md §11): a contest told as a match report — the definition, the rules,
/// the two teams' lineups and the events with their minute — and what the engine must make of it. Readable by people
/// who don't write code, so teams and players are named (KMSA's fictional clubs, docs/product/pilot-customer.md).
/// </summary>
internal sealed record Scenario(
    string Title,
    ScenarioDefinition Definition,
    string Rules,
    CoverageLevel Coverage,
    ScenarioTeam Home,
    ScenarioTeam Away,
    IReadOnlyList<ScenarioEvent> Events,
    ScenarioExpectation Expect)
{
    /// <summary>Rule values by dotted path, over the named rules' values (<c>substitutions.max: 3</c>).</summary>
    public Dictionary<string, JsonElement> RuleOverrides { get; init; } = [];

    public ContestStatus Status { get; init; } = ContestStatus.Live;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "Engines", "Scenarios");

    public static Scenario Read(string path) =>
        YamlJson.Parse(File.ReadAllText(path)).Deserialize<Scenario>(_options)
            ?? throw new InvalidOperationException($"{path} is empty.");

    /// <summary>The named rules (<c>Scenarios/rules/&lt;name&gt;.yaml</c>) flattened to dotted paths, then the overrides.</summary>
    public ContestRules ContestRules()
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        Flatten(YamlJson.Parse(File.ReadAllText(Path.Combine(Folder, "rules", $"{Rules}.yaml"))), null, values);
        foreach (var (path, value) in RuleOverrides)
        {
            values.Remove(path);
            Flatten(JsonNode.Parse(value.GetRawText()), path, values);
        }

        return new ContestRules(values);
    }

    public SportDefinition SportDefinition()
    {
        var files = new SportDefinitionFiles();
        var definition = files.Names
            .Select(name => SportDefinitionContent.Read(YamlJson.Parse(files.Read(name))!))
            .SingleOrDefault(candidate => candidate.Variant == Definition.Variant && candidate.Version == Definition.Version)
            ?? throw new InvalidOperationException($"No embedded definition {Definition.Variant}@{Definition.Version}.");
        return definition with { GenericEventTypes = GenericEventTypes.All };
    }

    // Objects become dotted paths; lists of text stay lists; numbers, flags and text are values.
    private static void Flatten(JsonNode? node, string? path, Dictionary<string, object> values)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var (key, child) in value)
                {
                    Flatten(child, path is null ? key : $"{path}.{key}", values);
                }

                break;
            case JsonArray list:
                values.Add(path!, list.Select(item => item!.GetValue<string>()).ToList());
                break;
            case JsonValue value when value.TryGetValue<long>(out var number):
                values.Add(path!, number);
                break;
            case JsonValue value when value.TryGetValue<bool>(out var flag):
                values.Add(path!, flag);
                break;
            case JsonValue value:
                values.Add(path!, value.GetValue<string>());
                break;
            case null when path is not null:
                break;
            default:
                throw new InvalidOperationException($"Rule {path} has no value.");
        }
    }
}

internal sealed record ScenarioDefinition(string Variant, int Version);

internal sealed record ScenarioTeam(string Team)
{
    public IReadOnlyList<string> Starters { get; init; } = [];

    public IReadOnlyList<string> Bench { get; init; } = [];

    public IReadOnlyList<string> Staff { get; init; } = [];
}

/// <summary>
/// One event. <see cref="Minute"/> is the match clock as written (<c>23</c>, <c>45+2</c>); <see cref="Id"/> names the
/// event for a later <see cref="Supersedes"/> or <see cref="Reverses"/>; <see cref="Rejected"/> lists the error codes
/// the engine must answer with, and a rejected event is not recorded.
/// </summary>
internal sealed record ScenarioEvent(string Type)
{
    public string? Id { get; init; }

    public JsonElement? Minute { get; init; }

    public string? Team { get; init; }

    public string? Period { get; init; }

    public Dictionary<string, string> Actors { get; init; } = [];

    public Dictionary<string, JsonElement> Payload { get; init; } = [];

    public string? Supersedes { get; init; }

    public string? Reverses { get; init; }

    public IReadOnlyList<string> Rejected { get; init; } = [];

    /// <summary>The clock in seconds and the added time in seconds (<c>45+2</c> → 2700 and 120).</summary>
    public (int? Clock, int? Added) Clock()
    {
        if (Minute is not { } minute)
        {
            return (null, null);
        }

        var parts = (minute.ValueKind == JsonValueKind.Number ? minute.GetInt32().ToString(CultureInfo.InvariantCulture) : minute.GetString()!).Split('+');
        var added = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) * 60 : (int?)null;
        return (int.Parse(parts[0], CultureInfo.InvariantCulture) * 60, added);
    }

    public string Describe(int index) => $"event {index + 1} ({Type}{(Minute is { } minute ? $", {minute}'" : string.Empty)})";
}

/// <summary>The score per team and the outcome when the last event has been recorded.</summary>
internal sealed record ScenarioExpectation(Dictionary<string, ExpectedScore> Score, ExpectedOutcome Outcome);

/// <summary>A team's <c>main</c> total, its shoot-out score when there was one, and its points per period short label.</summary>
internal sealed record ExpectedScore(int Total)
{
    public int? Shootout { get; init; }

    public Dictionary<string, int> Periods { get; init; } = [];
}

/// <summary>Either a winner and the method, or a draw.</summary>
internal sealed record ExpectedOutcome
{
    public string? Winner { get; init; }

    public OutcomeMethod? Method { get; init; }

    public bool Draw { get; init; }
}
