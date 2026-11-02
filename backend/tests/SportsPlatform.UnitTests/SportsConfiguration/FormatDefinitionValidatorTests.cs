using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// Each rejecting case edits a copy of an embedded format. JSON is YAML, so the edited tree goes back through the
// validator as text, like a file would. Each case lists every problem its edit causes.
public sealed class FormatDefinitionValidatorTests
{
    private const string _roundRobin = "round-robin.yaml";

    private const string _knockout = "knockout.yaml";

    private static readonly FormatDefinitionFiles _files = new();

    [Fact]
    public void The_embedded_formats_are_valid()
    {
        _files.Names.ShouldBe(["knockout.yaml", "round-robin.yaml", "single-contest.yaml"]);
        foreach (var file in _files.Names)
        {
            FormatDefinitionValidator.Validate(file, _files.Read(file)).ShouldBeEmpty(file);
        }
    }

    [Fact]
    public void Invalid_YAML_is_one_problem_at_the_root()
    {
        var problem = FormatDefinitionValidator.Validate(_roundRobin, "code: [ROUND_ROBIN").ShouldHaveSingleItem();

        problem.ToString().ShouldStartWith("round-robin.yaml: $: not valid YAML (line ");
    }

    public static TheoryData<string, string, Action<JsonObject>, string[]> Shapes => new()
    {
        {
            "a missing member", _roundRobin,
            root => root.Remove("participants"),
            ["round-robin.yaml: $: Required properties [\"participants\"] are not present"]
        },
        {
            "an unknown member", _roundRobin,
            root => root["stages"]![0]!["rounds"] = 3,
            ["round-robin.yaml: $.stages[0].rounds: unknown member"]
        },
        {
            "an unknown stage type", _roundRobin,
            root => root["stages"]![0]!["type"] = "POOL",
            ["round-robin.yaml: $.stages[0].type: Value should match one of the values specified by the enum"]
        },
        {
            "fewer than two participants", _roundRobin,
            root => root["participants"]!["min"] = 1,
            ["round-robin.yaml: $.participants.min: 1 should be at least 2"]
        },
        {
            "a parameter code that isn't lower snake case", _roundRobin,
            root => Rename(root, "cycles", "Cycles"),
            ["round-robin.yaml: $.parameters.Cycles: The string value is not a match for the indicated regular expression"]
        },
        {
            "a number parameter without bounds", _roundRobin,
            root => Parameter(root, "cycles").Remove("max"),
            ["round-robin.yaml: $.parameters.cycles: Required properties [\"max\"] are not present"]
        },
        {
            "a choice parameter without values", _roundRobin,
            root => Parameter(root, "seeding").Remove("values"),
            ["round-robin.yaml: $.parameters.seeding: Required properties [\"values\"] are not present"]
        },
        {
            "a default of another type", _roundRobin,
            root => Parameter(root, "home_away_balance")["default"] = "yes",
            ["round-robin.yaml: $.parameters.home_away_balance.default: Value is \"string\" but should be \"boolean\""]
        },
        // R1 (#82): the schema refuses what the table or the typed model can't hold, so the startup check catches it.
        {
            "a code the table's check refuses", _roundRobin,
            root => root["code"] = "ROUND_ROBIN_",
            ["round-robin.yaml: $.code: The string value is not a match for the indicated regular expression"]
        },
        {
            "a code longer than the column", _roundRobin,
            root => root["code"] = "ROUND_" + string.Concat(Enumerable.Repeat("ROBIN_", 8)) + "X",
            ["round-robin.yaml: $.code: Value should be at most 50 characters"]
        },
        {
            "a name longer than the column", _roundRobin,
            root => root["name"] = new string('L', 101),
            ["round-robin.yaml: $.name: Value should be at most 100 characters"]
        },
        {
            "more participants than a format takes", _roundRobin,
            root => root["participants"]!["max"] = 3000000000,
            ["round-robin.yaml: $.participants.max: 3000000000 should be at most 1024"]
        },
        {
            "a default beyond a 32-bit whole number", _roundRobin,
            root => Parameter(root, "cycles")["default"] = 3000000000,
            ["round-robin.yaml: $.parameters.cycles.default: 3000000000 should be at most 2147483647"]
        },
        {
            "bounds on a flag", _roundRobin,
            root => Parameter(root, "home_away_balance")["max"] = 1,
            ["round-robin.yaml: $.parameters.home_away_balance.max: not allowed for this type"]
        },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Shape_problems_name_the_file_the_path_and_the_problem(string shape, string file, Action<JsonObject> edit, string[] expected)
    {
        Validate(file, edit).Select(p => p.ToString()).ShouldBe(expected, shape);
    }

    public static TheoryData<string, string, Action<JsonObject>, string[]> Rules => new()
    {
        {
            "max is not below min", _roundRobin,
            root => root["participants"]!["min"] = 50,
            ["round-robin.yaml: $.participants.max: 40 is below min (50)"]
        },
        {
            "a one-stage format takes no more than its generator", "single-contest.yaml",
            root => root["participants"]!["max"] = 3,
            ["single-contest.yaml: $.participants.max: generator SINGLE_CONTEST takes at most 2"]
        },
        {
            "unique stage codes", _roundRobin,
            root => root["stages"]!.AsArray().Add(new JsonObject { ["code"] = "LEAGUE", ["type"] = "GROUP", ["generator"] = "ROUND_ROBIN" }),
            ["round-robin.yaml: $.stages[1].code: duplicate stage code \"LEAGUE\""]
        },
        {
            "a known generator", _roundRobin,
            root =>
            {
                root["stages"]![0]!["generator"] = "SWISS";
                root["parameters"] = new JsonObject();
            },
            ["round-robin.yaml: $.stages[0].generator: unknown generator \"SWISS\""]
        },
        {
            "the generator fills the stage's type", _roundRobin,
            root => root["stages"]![0]!["type"] = "KNOCKOUT",
            ["round-robin.yaml: $.stages[0].type: generator ROUND_ROBIN doesn't fill a KNOCKOUT stage"]
        },
        {
            "the generator pairs every compatible shape", _knockout,
            root => root["compatible_shapes"]!.AsArray().Add("FIELD"),
            ["knockout.yaml: $.compatible_shapes[1]: generator KNOCKOUT doesn't pair a FIELD contest"]
        },
        {
            "every parameter the generator reads is declared", _roundRobin,
            root => root["parameters"]!.AsObject().Remove("cycles"),
            ["round-robin.yaml: $.parameters: missing parameter \"cycles\", which the generator at $.stages[0].generator reads"]
        },
        {
            "every declared parameter is read", _knockout,
            root => root["parameters"]!["cycles"] = new JsonObject { ["type"] = "int", ["label"] = "Cycles", ["min"] = 1, ["max"] = 2, ["default"] = 1 },
            ["knockout.yaml: $.parameters.cycles: no stage's generator reads this parameter"]
        },
        {
            "a parameter has the type its generator reads", _knockout,
            root => root["parameters"]!["third_place_contest"] = new JsonObject { ["type"] = "int", ["label"] = "Third place", ["min"] = 0, ["max"] = 1, ["default"] = 0 },
            ["knockout.yaml: $.parameters.third_place_contest.type: the generator reads it as bool, not int"]
        },
        {
            "a number's bounds are in order", _roundRobin,
            root => Parameter(root, "cycles")["min"] = 5,
            ["round-robin.yaml: $.parameters.cycles.max: 4 is below min (5)"]
        },
        {
            "a number's default is within its bounds", _roundRobin,
            root => Parameter(root, "cycles")["default"] = 5,
            ["round-robin.yaml: $.parameters.cycles.default: 5 is outside 1..4"]
        },
        {
            "a choice's values are ones the generator understands", _roundRobin,
            root => Parameter(root, "seeding")["values"]!.AsArray().Add("SNAKE"),
            ["round-robin.yaml: $.parameters.seeding.values: the generator doesn't understand \"SNAKE\""]
        },
        {
            "a choice's default is one of its values", _knockout,
            root => Parameter(root, "seeding")["values"] = new JsonArray("RANDOM", "MANUAL"),
            ["knockout.yaml: $.parameters.seeding.default: \"SEEDED\" is not one of the values"]
        },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_rule_names_the_file_the_path_and_the_problem(string rule, string file, Action<JsonObject> edit, string[] expected)
    {
        Validate(file, edit).Select(p => p.ToString()).ShouldBe(expected, rule);
    }

    private static IReadOnlyList<DefinitionProblem> Validate(string file, Action<JsonObject> edit)
    {
        var root = YamlJson.Parse(_files.Read(file))!.AsObject();
        edit(root);
        return FormatDefinitionValidator.Validate(file, root.ToJsonString());
    }

    private static JsonObject Parameter(JsonObject root, string code) => root["parameters"]![code]!.AsObject();

    private static void Rename(JsonObject root, string code, string newCode)
    {
        var parameters = root["parameters"]!.AsObject();
        var parameter = parameters[code]!;
        parameters.Remove(code);
        parameters[newCode] = parameter;
    }
}
