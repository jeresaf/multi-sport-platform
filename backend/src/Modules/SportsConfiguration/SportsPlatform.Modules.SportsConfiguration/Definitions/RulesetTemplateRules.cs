using System.Text.Json;
using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The rules JSON Schema can't express for a ruleset template (S05 D3, D11): the sport definition at the pinned version
/// exists; every format exists and pairs the sport's contest shape; each editable parameter names a value of its type,
/// with bounds in order that hold the value, and allowed codes the sport and the catalogue know; and the values keep
/// <see cref="RulesetValueChecks"/>. Runs only on a file whose shape is valid.
/// </summary>
internal sealed class RulesetTemplateRules
{
    private readonly string _file;
    private readonly JsonObject _root;
    private readonly RulesetReferences _references;
    private readonly List<DefinitionProblem> _problems = [];

    private RulesetTemplateRules(string file, JsonObject root, RulesetReferences references)
    {
        _file = file;
        _root = root;
        _references = references;
    }

    public static IReadOnlyList<DefinitionProblem> Check(string file, JsonObject root, RulesetReferences references)
    {
        var rules = new RulesetTemplateRules(file, root, references);
        rules.Run();
        return rules._problems;
    }

    private void Run()
    {
        var reference = _root["sport"]!;
        var (variant, version) = (Text(reference["variant"]), (int)Number(reference["version"]));
        var sport = _references.Sport(variant, version);
        if (sport is null)
        {
            Problem("$.sport", $"no sport definition {variant}@{version}");
        }

        var formats = _root["formats"]!.AsArray();
        for (var index = 0; index < formats.Count; index++)
        {
            var code = Text(formats[index]);
            var format = _references.Format(code);
            if (format is null)
            {
                Problem($"$.formats[{index}]", $"unknown format \"{code}\"");
            }
            else if (sport is not null && !format.CompatibleShapes.Contains(sport.Shape))
            {
                Problem($"$.formats[{index}]", $"format {code} doesn't take {RulesetValueChecks.Upper(sport.Shape)} contests");
            }
        }

        var values = _root["values"]!;
        foreach (var (path, parameter) in _root["editable"]!.AsObject())
        {
            Editable(path, parameter!.AsObject(), values, sport);
        }

        if (sport is not null)
        {
            foreach (var problem in RulesetValueChecks.Check(RulesetTemplateContent.ReadValues(values), sport, _references.Tiebreaker))
            {
                Problem($"$.values.{problem.Path}", problem.Message);
            }
        }
    }

    private void Editable(string path, JsonObject parameter, JsonNode values, SportDefinition? sport)
    {
        var at = $"$.editable.{path}";
        if (!RulesetTemplateContent.TryGetValue(values, path, out var value))
        {
            Problem(at, $"no value at values.{path}");
            return;
        }

        var type = Text(parameter["type"]);
        if (!Fits(type, value))
        {
            Problem(at, $"the value at values.{path} is not {(type is "int" or "enum" ? "an" : "a")} {type}");
            return;
        }

        switch (type)
        {
            case "int":
                var (min, max, number) = (Number(parameter["min"]), Number(parameter["max"]), Number(value));
                if (max < min)
                {
                    Problem($"{at}.max", $"{max} is below min ({min})");
                }
                else if (number < min || number > max)
                {
                    Problem(at, $"the value at values.{path} ({number}) is outside {min}..{max}");
                }

                break;
            case "enum" or "list":
                var allowed = parameter["values"]!.AsArray().Select(Text).ToList();
                IEnumerable<string> current = type == "enum" ? [Text(value)] : value!.AsArray().Select(Text);
                foreach (var code in current.Where(code => !allowed.Contains(code, StringComparer.Ordinal)))
                {
                    Problem(at, $"the value at values.{path} has \"{code}\", which is not one of the values");
                }

                if (path is not (RulesetValueChecks.StandingsTiebreakersPath or RulesetValueChecks.TieResolutionPath or RulesetValueChecks.CoverageDefaultPath))
                {
                    Problem(at, $"values.{path} can't be a tenant's choice: only {RulesetValueChecks.StandingsTiebreakersPath}, " +
                        $"{RulesetValueChecks.TieResolutionPath} and {RulesetValueChecks.CoverageDefaultPath} are");
                }
                else if (sport is not null)
                {
                    AllowedCodes($"{at}.values", path, allowed, sport);
                }

                break;
        }
    }

    // Allowed codes a tenant could pick must be codes the sport supports, as the values' own are. Only paths whose codes
    // are checked here may be choices or lists: any other would let a tenant pick a code nothing checks.
    private void AllowedCodes(string at, string path, List<string> allowed, SportDefinition sport)
    {
        foreach (var code in allowed)
        {
            var problem = path switch
            {
                RulesetValueChecks.StandingsTiebreakersPath => _references.Tiebreaker(code) switch
                {
                    null => $"unknown tiebreaker \"{code}\"",
                    { } tiebreaker when !tiebreaker.AppliesTo(sport.Sport) => $"tiebreaker \"{code}\" doesn't apply to {sport.Name}",
                    _ => null,
                },
                RulesetValueChecks.TieResolutionPath when !sport.Supports.TieResolutions.Contains(code, StringComparer.Ordinal) =>
                    $"the sport doesn't support \"{code}\"",
                RulesetValueChecks.CoverageDefaultPath when !sport.CoverageLevels.Any(level => RulesetValueChecks.Upper(level) == code) =>
                    $"the sport has no {code} coverage",
                _ => null,
            };
            if (problem is not null)
            {
                Problem(at, problem);
            }
        }
    }

    private static bool Fits(string type, JsonNode? value) => type switch
    {
        "int" => value is JsonValue number && number.GetValueKind() == JsonValueKind.Number,
        "bool" => value is JsonValue flag && flag.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "enum" => value is JsonValue text && text.GetValueKind() == JsonValueKind.String,
        "list" => value is JsonArray list && list.All(item => item is JsonValue text && text.GetValueKind() == JsonValueKind.String),
        _ => false,
    };

    private void Problem(string path, string message) => _problems.Add(new DefinitionProblem(_file, path, message));

    private static string Text(JsonNode? node) => node!.GetValue<string>();

    // YamlJson reads whole numbers as long.
    private static long Number(JsonNode? node) => node!.GetValue<long>();
}
