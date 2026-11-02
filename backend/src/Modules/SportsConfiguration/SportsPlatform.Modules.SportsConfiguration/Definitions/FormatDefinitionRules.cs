using System.Text.Json;
using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The rules JSON Schema can't express for a format (S05 D3, D10): unique stage codes; each stage's generator is known
/// (<see cref="FormatGeneratorKinds"/>), fills the stage's type and pairs every compatible shape; a one-stage format's
/// participant range is one its generator takes; the format declares exactly the parameters its generators read, with
/// their types and only values they understand; each parameter's bounds hold its default. Runs only on a file whose
/// shape is valid.
/// </summary>
internal sealed class FormatDefinitionRules
{
    private readonly string _file;
    private readonly JsonObject _root;
    private readonly List<DefinitionProblem> _problems = [];

    private FormatDefinitionRules(string file, JsonObject root)
    {
        _file = file;
        _root = root;
    }

    public static IReadOnlyList<DefinitionProblem> Check(string file, JsonObject root)
    {
        var rules = new FormatDefinitionRules(file, root);
        rules.Run();
        return rules._problems;
    }

    private void Run()
    {
        var participants = _root["participants"]!.AsObject();
        var (min, max) = (Number(participants["min"]), Number(participants["max"]));
        if (max < min)
        {
            Problem("$.participants.max", $"{max} is below min ({min})");
        }

        var stages = _root["stages"]!.AsArray();
        var declared = _root["parameters"]!.AsObject();
        var read = new Dictionary<string, (GeneratorParameter Parameter, string Path)>(StringComparer.Ordinal);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < stages.Count; index++)
        {
            var path = $"$.stages[{index}]";
            var stage = stages[index]!.AsObject();
            var code = Text(stage["code"]);
            if (!codes.Add(code))
            {
                Problem($"{path}.code", $"duplicate stage code \"{code}\"");
            }

            var generatorCode = Text(stage["generator"]);
            if (!FormatGeneratorKinds.Known.TryGetValue(generatorCode, out var generator))
            {
                Problem($"{path}.generator", $"unknown generator \"{generatorCode}\"");
                continue;
            }

            Stage(path, stage, generatorCode, generator, stages.Count == 1 ? (min, max) : null);
            foreach (var (name, parameter) in generator.Parameters)
            {
                read.TryAdd(name, (parameter, $"{path}.generator"));
            }
        }

        foreach (var (name, (parameter, path)) in read)
        {
            if (declared[name] is not JsonObject definition)
            {
                Problem("$.parameters", $"missing parameter \"{name}\", which the generator at {path} reads");
                continue;
            }

            Parameter($"$.parameters.{name}", definition, parameter);
        }

        foreach (var (name, _) in declared.Where(parameter => !read.ContainsKey(parameter.Key)))
        {
            Problem($"$.parameters.{name}", "no stage's generator reads this parameter");
        }
    }

    private void Stage(string path, JsonObject stage, string code, FormatGeneratorKind generator, (long Min, long Max)? participants)
    {
        var type = Text(stage["type"]);
        if (!generator.StageTypes.Any(stageType => Upper(stageType) == type))
        {
            Problem($"{path}.type", $"generator {code} doesn't fill a {type} stage");
        }

        var shapes = _root["compatible_shapes"]!.AsArray();
        for (var index = 0; index < shapes.Count; index++)
        {
            var shape = Text(shapes[index]);
            if (!generator.Shapes.Any(known => Upper(known) == shape))
            {
                Problem($"$.compatible_shapes[{index}]", $"generator {code} doesn't pair a {shape} contest");
            }
        }

        // Only a one-stage format's participants all reach its generator; a later stage takes the earlier one's qualifiers.
        if (participants is { } range)
        {
            if (range.Min < generator.MinParticipants)
            {
                Problem("$.participants.min", $"generator {code} needs at least {generator.MinParticipants}");
            }

            if (range.Max > generator.MaxParticipants)
            {
                Problem("$.participants.max", $"generator {code} takes at most {generator.MaxParticipants}");
            }
        }
    }

    private void Parameter(string path, JsonObject definition, GeneratorParameter read)
    {
        var type = Text(definition["type"]);
        var expected = JsonNamingPolicyName(read.Type);
        if (type != expected)
        {
            Problem($"{path}.type", $"the generator reads it as {expected}, not {type}");
            return;
        }

        var defaultValue = definition["default"]!;
        switch (read.Type)
        {
            case ParameterType.Number:
                var (min, max, value) = (Number(definition["min"]), Number(definition["max"]), Number(defaultValue));
                if (max < min)
                {
                    Problem($"{path}.max", $"{max} is below min ({min})");
                }
                else if (value < min || value > max)
                {
                    Problem($"{path}.default", $"{value} is outside {min}..{max}");
                }

                break;
            case ParameterType.Choice:
                var values = definition["values"]!.AsArray().Select(Text).ToList();
                foreach (var unknown in values.Where(choice => !read.Values.Contains(choice, StringComparer.Ordinal)))
                {
                    Problem($"{path}.values", $"the generator doesn't understand \"{unknown}\"");
                }

                if (!values.Contains(Text(defaultValue), StringComparer.Ordinal))
                {
                    Problem($"{path}.default", $"\"{Text(defaultValue)}\" is not one of the values");
                }

                break;
            case ParameterType.Flag:
                break;
            default:
                throw new InvalidOperationException($"Unknown parameter type {read.Type}.");
        }
    }

    private void Problem(string path, string message) => _problems.Add(new DefinitionProblem(_file, path, message));

    private static string Text(JsonNode? node) => node!.GetValue<string>();

    // YamlJson reads whole numbers as long.
    private static long Number(JsonNode? node) => node!.GetValue<long>();

    private static string Upper<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    // The files' names for the types (int, bool, enum).
    private static string JsonNamingPolicyName(ParameterType type) => type switch
    {
        ParameterType.Number => "int",
        ParameterType.Flag => "bool",
        ParameterType.Choice => "enum",
        _ => throw new InvalidOperationException($"Unknown parameter type {type}."),
    };
}
