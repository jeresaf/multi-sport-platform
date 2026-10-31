using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using Resolutions = SportsPlatform.Modules.SportsConfiguration.Contracts.Engines.TieResolutions;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The rules JSON Schema can't express: unique codes and the references between a definition's lists (S05 D3). Runs
/// only on a file whose shape is valid, so required members are present and have the right types.
/// </summary>
internal sealed class SportDefinitionRules
{
    private static readonly string[] _codedLists = ["periods", "event_types", "positions", "staff_roles", "official_roles", "statistics"];

    // A statistic is derived in exactly one of these ways (docs/domain/sports/README.md, "statistics").
    private static readonly string[] _deriveForms = ["count_events", "sum_points", "builtin", "manual"];

    private readonly string _file;
    private readonly JsonObject _root;
    private readonly List<DefinitionProblem> _problems = [];

    private SportDefinitionRules(string file, JsonObject root)
    {
        _file = file;
        _root = root;
    }

    public static IReadOnlyList<DefinitionProblem> Check(string file, JsonObject root)
    {
        var rules = new SportDefinitionRules(file, root);
        rules.Run();
        return rules._problems;
    }

    private void Run()
    {
        foreach (var list in _codedLists)
        {
            UniqueCodes(list);
        }

        var roles = _root["actor_roles"]!.AsObject().Select(role => role.Key).ToHashSet(StringComparer.Ordinal);
        var coverage = Strings(_root["coverage_levels"]).ToHashSet(StringComparer.Ordinal);
        var phaseKinds = Items("periods").Select(period => Text(period.Item, "kind")).ToHashSet(StringComparer.Ordinal);
        var events = Codes("event_types");
        var eventsOrGeneric = events.Union(GenericEventTypes.Codes).ToHashSet(StringComparer.Ordinal);
        var family = ScoringFamilies.Known.GetValueOrDefault(Text(_root, "scoring_family"));
        if (family is null)
        {
            Problem("$.scoring_family", $"scoring family \"{Text(_root, "scoring_family")}\" has no engine yet");
        }

        foreach (var (item, path) in Items("event_types"))
        {
            EventType(item, path, roles, coverage, phaseKinds, events, eventsOrGeneric, family);
        }

        PositionGroups();
        Statistics(roles, coverage, events);
        ScorerPad(coverage, phaseKinds, eventsOrGeneric);
        if (family is not null)
        {
            TieResolutions(family);
        }
    }

    private void UniqueCodes(string list)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (item, path) in Items(list))
        {
            var code = Text(item, "code");
            if (!seen.TryAdd(code, path))
            {
                Problem($"{path}.code", $"duplicate code \"{code}\" (also at {seen[code]})");
            }
            else if (list == "event_types" && GenericEventTypes.Codes.Contains(code))
            {
                Problem($"{path}.code", $"\"{code}\" is a generic event type and can't be redefined");
            }
        }
    }

    private void EventType(
        JsonObject item,
        string path,
        HashSet<string> roles,
        HashSet<string> coverage,
        HashSet<string> phaseKinds,
        HashSet<string> events,
        HashSet<string> eventsOrGeneric,
        ScoringFamily? family)
    {
        Known(coverage, Text(item, "coverage"), $"{path}.coverage", "coverage level");
        foreach (var (phase, index) in Strings(item["phases"]).Select((phase, index) => (phase, index)))
        {
            if (!phaseKinds.Contains(phase))
            {
                Problem($"{path}.phases[{index}]", $"no period has kind \"{phase}\"");
            }
        }

        var eventRoles = Items(item["actors"] as JsonArray, $"{path}.actors").Select(actor => Text(actor.Item, "role")).ToHashSet(StringComparer.Ordinal);
        foreach (var (actor, actorPath) in Items(item["actors"] as JsonArray, $"{path}.actors"))
        {
            Known(roles, Text(actor, "role"), $"{actorPath}.role", "actor role");
            if (OptionalText(actor, "required_from") is { } requiredFrom)
            {
                Known(coverage, requiredFrom, $"{actorPath}.required_from", "coverage level");
            }

            if (OptionalText(actor, "distinct_from") is { } distinctFrom && !eventRoles.Contains(distinctFrom))
            {
                Problem($"{actorPath}.distinct_from", $"\"{distinctFrom}\" is not a role on this event");
            }
        }

        foreach (var (name, field) in item["metadata"] as JsonObject ?? [])
        {
            MetadataField(field!.AsObject(), $"{path}.metadata.{name}");
        }

        if (item["follow_up"] is JsonObject followUp)
        {
            KnownAll(events, followUp["requires_preceding"], $"{path}.follow_up.requires_preceding", "event type");
        }

        KnownAll(eventsOrGeneric, item["reversible_by"], $"{path}.reversible_by", "event type");
        if (family is not null
            && OptionalText(item, "component") is { } component
            && component != ScoringFamilies.MainComponent
            && !family.ScoreComponents.Contains(component))
        {
            Problem($"{path}.component", $"unknown score component \"{component}\" for scoring family {Text(_root, "scoring_family")}");
        }
    }

    private void MetadataField(JsonObject field, string path)
    {
        var type = Text(field, "type");
        if (type == "enum" && field["values"] is null)
        {
            Problem(path, "an enum field needs values");
        }
        else if (type != "enum" && field["values"] is not null)
        {
            Problem($"{path}.values", $"values are only for enum fields, not {type}");
        }

        if (type != "int" && (field["min"] is not null || field["max"] is not null))
        {
            Problem(path, $"min and max are only for int fields, not {type}");
        }
    }

    // A constraint names a position group or a single position (docs/domain/sports/README.md).
    private void PositionGroups()
    {
        var positions = Items("positions")
            .SelectMany(position => new[] { Text(position.Item, "group"), Text(position.Item, "code") })
            .ToHashSet(StringComparer.Ordinal);
        var lineup = _root["lineup"]!.AsObject();
        foreach (var (constraint, path) in Items(lineup["position_constraints"] as JsonArray, "$.lineup.position_constraints"))
        {
            Known(positions, Text(constraint, "group"), $"{path}.group", "position group");
        }
    }

    private void Statistics(HashSet<string> roles, HashSet<string> coverage, HashSet<string> events)
    {
        var statistics = Codes("statistics");
        foreach (var (statistic, path) in Items("statistics"))
        {
            Known(coverage, Text(statistic, "coverage"), $"{path}.coverage", "coverage level");
            var derive = statistic["derive"]!.AsObject();
            var forms = _deriveForms.Where(derive.ContainsKey).ToList();
            if (forms.Count != 1)
            {
                Problem($"{path}.derive", $"needs exactly one of {string.Join(", ", _deriveForms)}"
                    + (forms.Count > 1 ? $" (found {string.Join(", ", forms)})" : string.Empty));
            }

            // Only once the form is clear: a ratio without builtin is already "needs exactly one of …".
            var ratio = derive.ContainsKey("numerator") || derive.ContainsKey("denominator");
            if (forms.Count == 1 && ratio && !(derive.ContainsKey("numerator") && derive.ContainsKey("denominator") && OptionalText(derive, "builtin") == "RATIO"))
            {
                Problem($"{path}.derive", "numerator and denominator go together, with builtin RATIO");
            }

            KnownAll(events, derive["count_events"], $"{path}.derive.count_events", "event type");
            KnownAll(events, derive["sum_points"], $"{path}.derive.sum_points", "event type");
            KnownAll(roles, derive["actor_roles"], $"{path}.derive.actor_roles", "actor role");
            foreach (var part in new[] { "numerator", "denominator" })
            {
                if (OptionalText(derive, part) is { } code)
                {
                    Known(statistics, code, $"{path}.derive.{part}", "statistic");
                }
            }
        }
    }

    private void ScorerPad(HashSet<string> coverage, HashSet<string> phaseKinds, HashSet<string> eventsOrGeneric)
    {
        var pad = _root["scorer_pad"]!.AsObject();
        foreach (var (group, path) in Items(pad["groups"] as JsonArray, "$.scorer_pad.groups"))
        {
            KnownAll(eventsOrGeneric, group["buttons"], $"{path}.buttons", "event type");
            KnownAll(phaseKinds, group["phases"], $"{path}.phases", "period kind");
            if (OptionalText(group, "coverage") is { } level)
            {
                Known(coverage, level, $"{path}.coverage", "coverage level");
            }
        }
    }

    // Each tie resolution needs the periods the engine plays it with (docs/architecture/sport-engine.md §5).
    private void TieResolutions(ScoringFamily family)
    {
        var periods = Items("periods").Select(period => period.Item).ToList();
        bool Has(Func<JsonObject, bool> predicate) => periods.Any(predicate);
        bool Overtime(JsonObject period) => Text(period, "kind") == "PLAY" && Flag(period, "overtime");

        foreach (var (resolution, index) in Strings(_root["supports"]!["tie_resolutions"]).Select((value, index) => (value, index)))
        {
            var path = $"$.supports.tie_resolutions[{index}]";
            if (!family.TieResolutions.Contains(resolution))
            {
                Problem(path, $"unknown tie resolution \"{resolution}\" for scoring family {Text(_root, "scoring_family")}");
                continue;
            }

            if ((resolution is Resolutions.ExtraPeriodsThenShootout or Resolutions.Shootout) && !Has(period => Text(period, "kind") == "SHOOTOUT"))
            {
                Problem(path, $"tie resolution \"{resolution}\" needs a period of kind SHOOTOUT");
            }

            if ((resolution is Resolutions.ExtraPeriodsThenShootout or Resolutions.ExtraPeriodsSuddenDeath) && !Has(Overtime))
            {
                Problem(path, $"tie resolution \"{resolution}\" needs an overtime PLAY period");
            }

            // The first score ends it, and scoring happens only in PLAY periods.
            if (resolution == Resolutions.ExtraPeriodsSuddenDeath && !Has(period => Overtime(period) && Flag(period, "sudden_death")))
            {
                Problem(path, $"tie resolution \"{resolution}\" needs a sudden_death overtime PLAY period");
            }

            if (resolution == Resolutions.RepeatExtraPeriod && !Has(period => Overtime(period) && Flag(period, "repeatable")))
            {
                Problem(path, $"tie resolution \"{resolution}\" needs a repeatable overtime PLAY period");
            }
        }
    }

    private void Known(HashSet<string> known, string value, string path, string what)
    {
        if (!known.Contains(value))
        {
            Problem(path, $"unknown {what} \"{value}\"");
        }
    }

    private void KnownAll(HashSet<string> known, JsonNode? values, string path, string what)
    {
        foreach (var (value, index) in Strings(values).Select((value, index) => (value, index)))
        {
            Known(known, value, $"{path}[{index}]", what);
        }
    }

    private void Problem(string path, string message) => _problems.Add(new(_file, path, message));

    private HashSet<string> Codes(string list) =>
        Items(list).Select(item => Text(item.Item, "code")).ToHashSet(StringComparer.Ordinal);

    private IEnumerable<(JsonObject Item, string Path)> Items(string list) => Items(_root[list] as JsonArray, $"$.{list}");

    private static IEnumerable<(JsonObject Item, string Path)> Items(JsonArray? array, string path) =>
        (array ?? []).Select((item, index) => (item!.AsObject(), $"{path}[{index}]"));

    private static IEnumerable<string> Strings(JsonNode? array) =>
        (array as JsonArray ?? []).Select(item => item!.GetValue<string>());

    private static string Text(JsonObject item, string name) => item[name]!.GetValue<string>();

    private static string? OptionalText(JsonObject item, string name) => item[name]?.GetValue<string>();

    private static bool Flag(JsonObject item, string name) => item[name]?.GetValue<bool>() ?? false;
}
