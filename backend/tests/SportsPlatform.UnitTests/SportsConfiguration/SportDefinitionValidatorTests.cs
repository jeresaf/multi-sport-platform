using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// Each rejecting case edits a copy of the embedded football definition. JSON is YAML, so the edited tree goes back
// through the validator as text, like a file would.
public sealed class SportDefinitionValidatorTests
{
    private const string _file = "football.yaml";

    private static readonly SportDefinitionFiles _files = new();

    [Fact]
    public void The_embedded_definitions_are_valid()
    {
        _files.Names.ShouldContain(_file);
        foreach (var file in _files.Names)
        {
            SportDefinitionValidator.Validate(file, _files.Read(file)).ShouldBeEmpty();
        }
    }

    [Fact]
    public void Invalid_YAML_is_one_problem_at_the_root()
    {
        var problem = SportDefinitionValidator.Validate(_file, "variant: [FOOTBALL_ASSOCIATION").ShouldHaveSingleItem();

        problem.Path.ShouldBe("$");
        problem.Message.ShouldStartWith("not valid YAML (line ");
    }

    [Fact]
    public void A_duplicate_key_is_invalid_YAML()
    {
        SportDefinitionValidator.Validate(_file, "version: 1\nversion: 2\n").ShouldHaveSingleItem().Message.ShouldStartWith("not valid YAML");
    }

    [Fact]
    public void Plain_scalars_are_typed_and_quoted_ones_stay_text()
    {
        var root = YamlJson.Parse("a: 1\nb: '1'\nc: true\nd: 1H\ne: ~\nf: 2.5\n")!.AsObject();

        root["a"]!.GetValue<long>().ShouldBe(1);
        root["b"]!.GetValue<string>().ShouldBe("1");
        root["c"]!.GetValue<bool>().ShouldBeTrue();
        root["d"]!.GetValue<string>().ShouldBe("1H");
        root["e"].ShouldBeNull();
        root["f"]!.GetValue<decimal>().ShouldBe(2.5m);
    }

    [Fact]
    public void A_str_tag_keeps_a_scalar_text_and_other_tags_are_refused()
    {
        YamlJson.Parse("a: !!str 1\n")!["a"]!.GetValue<string>().ShouldBe("1");

        SportDefinitionValidator.Validate(_file, "version: !!int 1\n").ShouldHaveSingleItem().Message.ShouldContain("tag:yaml.org,2002:int is not supported");
    }

    [Fact]
    public void A_number_too_large_to_read_is_a_shape_problem_at_its_path()
    {
        var yaml = _files.Read(_file).Replace("\nversion: 1\n", "\nversion: 99999999999999999999999999999999\n", StringComparison.Ordinal);

        SportDefinitionValidator.Validate(_file, yaml).ShouldHaveSingleItem().Path.ShouldBe("$.version");
    }

    public static TheoryData<string, Action<JsonObject>, string[]> Shapes => new()
    {
        {
            "a missing member",
            root => root.Remove("scoring_family"),
            ["football.yaml: $: Required properties [\"scoring_family\"] are not present"]
        },
        {
            "an unknown member",
            root => Event(root, "GOAL")["pointz"] = 1,
            ["football.yaml: $.event_types[0].pointz: unknown member"]
        },
        {
            "a wrongly typed value",
            root => root["periods"]![0]!["default_seconds"] = "45 minutes",
            ["football.yaml: $.periods[0].default_seconds: Value is \"string\" but should be \"integer\""]
        },
        {
            "a duplicate in a list",
            root => root["coverage_levels"]!.AsArray().Add("BASIC"),
            ["football.yaml: $.coverage_levels: Found duplicates at the following index pairs: [\"(1,4)\"]"]
        },
        {
            "a head-to-head sport without sides",
            root => root.Remove("sides"),
            ["football.yaml: $: Required properties [\"sides\"] are not present"]
        },
        {
            // A failing "if" (shape is not HEAD_TO_HEAD) is not a problem: only the unrelated one is reported.
            "a field sport with another problem",
            root =>
            {
                root["shape"] = "FIELD";
                root.Remove("name");
            },
            ["football.yaml: $: Required properties [\"name\"] are not present"]
        },
        {
            // The reference rules run only on a valid shape.
            "a shape problem with a reference problem",
            root =>
            {
                root.Remove("name");
                Event(root, "GOAL")["actors"]![0]!["role"] = "SCORERR";
            },
            ["football.yaml: $: Required properties [\"name\"] are not present"]
        },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Shape_problems_name_the_file_the_path_and_the_problem(string shape, Action<JsonObject> edit, string[] expected)
    {
        Validate(edit).Select(p => p.ToString()).ShouldBe(expected, shape);
    }

    // Each case lists every problem its edit causes.
    public static TheoryData<string, Action<JsonObject>, string[]> Rules => new()
    {
        {
            "unique codes",
            root => root["positions"]!.AsArray().Add(new JsonObject { ["code"] = "GK", ["label"] = "Keeper", ["group"] = "GOALKEEPER" }),
            ["football.yaml: $.positions[12].code: duplicate code \"GK\" (also at $.positions[0])"]
        },
        {
            "event codes don't redefine generic event types",
            root => Event(root, "CORNER")["code"] = "NOTE",
            [
                "football.yaml: $.event_types[15].code: \"NOTE\" is a generic event type and can't be redefined",
                "football.yaml: $.statistics[14].derive.count_events[0]: unknown event type \"CORNER\"",
                "football.yaml: $.scorer_pad.groups[4].buttons[2]: unknown event type \"CORNER\"",
            ]
        },
        {
            "event actor role",
            root => Event(root, "PENALTY_GOAL")["actors"]![0]!["role"] = "TAKERR",
            ["football.yaml: $.event_types[1].actors[0].role: unknown actor role \"TAKERR\""]
        },
        {
            "event distinct_from is a role on the same event",
            root => Event(root, "GOAL")["actors"]![1]!["distinct_from"] = "TAKER",
            ["football.yaml: $.event_types[0].actors[1].distinct_from: \"TAKER\" is not a role on this event"]
        },
        {
            "event actor required_from is one of the definition's coverage levels",
            root => WithoutResultOnly(root, root => Event(root, "GOAL")["actors"]![0]!["required_from"] = "RESULT_ONLY"),
            ["football.yaml: $.event_types[0].actors[0].required_from: unknown coverage level \"RESULT_ONLY\""]
        },
        {
            "event coverage is one of the definition's coverage levels",
            root => WithoutResultOnly(root, root => Event(root, "CORNER")["coverage"] = "RESULT_ONLY"),
            ["football.yaml: $.event_types[15].coverage: unknown coverage level \"RESULT_ONLY\""]
        },
        {
            "event and scorer pad phases are kinds some period has",
            root => RemovePeriods(root, "SHOOTOUT"),
            [
                "football.yaml: $.event_types[4].phases[0]: no period has kind \"SHOOTOUT\"",
                "football.yaml: $.event_types[5].phases[0]: no period has kind \"SHOOTOUT\"",
                "football.yaml: $.event_types[6].phases[2]: no period has kind \"SHOOTOUT\"",
                "football.yaml: $.event_types[7].phases[2]: no period has kind \"SHOOTOUT\"",
                "football.yaml: $.event_types[8].phases[2]: no period has kind \"SHOOTOUT\"",
                "football.yaml: $.scorer_pad.groups[3].phases[0]: unknown period kind \"SHOOTOUT\"",
                "football.yaml: $.supports.tie_resolutions[1]: tie resolution \"EXTRA_PERIODS_THEN_SHOOTOUT\" needs a period of kind SHOOTOUT",
                "football.yaml: $.supports.tie_resolutions[2]: tie resolution \"SHOOTOUT\" needs a period of kind SHOOTOUT",
            ]
        },
        {
            "an enum metadata field has values",
            root => Event(root, "GOAL")["metadata"]!["body_part"]!.AsObject().Remove("values"),
            ["football.yaml: $.event_types[0].metadata.body_part: an enum field needs values"]
        },
        {
            "only an enum metadata field has values",
            root => Event(root, "SHOT")["metadata"]!["on_target"]!["values"] = new JsonArray("YES", "NO"),
            ["football.yaml: $.event_types[13].metadata.on_target.values: values are only for enum fields, not bool"]
        },
        {
            "only an int metadata field has min and max",
            root => Event(root, "SHOT")["metadata"]!["blocked"]!["max"] = 1,
            ["football.yaml: $.event_types[13].metadata.blocked: min and max are only for int fields, not bool"]
        },
        {
            "position constraint group",
            root => root["lineup"]!["position_constraints"]![0]!["group"] = "KEEPER",
            ["football.yaml: $.lineup.position_constraints[0].group: unknown position group \"KEEPER\""]
        },
        {
            "statistic counted events",
            root => Statistic(root, "GOALS")["derive"]!["count_events"]![1] = "PENALTY_GOALS",
            ["football.yaml: $.statistics[0].derive.count_events[1]: unknown event type \"PENALTY_GOALS\""]
        },
        {
            "statistic summed events",
            root => Statistic(root, "GOALS")["derive"] = new JsonObject
            {
                ["sum_points"] = new JsonArray("GOAL", "PENALTY_GOALS"), ["actor_roles"] = new JsonArray("SCORER", "TAKER"),
            },
            ["football.yaml: $.statistics[0].derive.sum_points[1]: unknown event type \"PENALTY_GOALS\""]
        },
        {
            "statistic actor roles",
            root => Statistic(root, "GOALS")["derive"]!["actor_roles"]![0] = "STRIKER",
            ["football.yaml: $.statistics[0].derive.actor_roles[0]: unknown actor role \"STRIKER\""]
        },
        {
            "statistic ratio numerator",
            root => Statistic(root, "POSSESSION_PERCENT")["derive"] = Ratio("SHOTS_SCORED", "SHOTS"),
            ["football.yaml: $.statistics[16].derive.numerator: unknown statistic \"SHOTS_SCORED\""]
        },
        {
            "statistic ratio denominator",
            root => Statistic(root, "POSSESSION_PERCENT")["derive"] = Ratio("SHOTS_ON_TARGET", "SHOTS_TAKEN"),
            ["football.yaml: $.statistics[16].derive.denominator: unknown statistic \"SHOTS_TAKEN\""]
        },
        {
            "a ratio has both parts and builtin RATIO",
            root => Statistic(root, "POSSESSION_PERCENT")["derive"] = new JsonObject { ["builtin"] = "RATIO", ["numerator"] = "SHOTS_ON_TARGET" },
            ["football.yaml: $.statistics[16].derive: numerator and denominator go together, with builtin RATIO"]
        },
        {
            "a statistic is derived one way",
            root => Statistic(root, "GOALS")["derive"]!["builtin"] = "GOALS",
            ["football.yaml: $.statistics[0].derive: needs exactly one of count_events, sum_points, builtin, manual (found count_events, builtin)"]
        },
        {
            "a statistic is derived some way",
            root => Statistic(root, "CORNERS")["derive"] = new JsonObject { ["actor_roles"] = new JsonArray("PLAYER") },
            ["football.yaml: $.statistics[14].derive: needs exactly one of count_events, sum_points, builtin, manual"]
        },
        {
            "statistic coverage is one of the definition's coverage levels",
            root => WithoutResultOnly(root, root => Statistic(root, "GOALS")["coverage"] = "RESULT_ONLY"),
            ["football.yaml: $.statistics[0].coverage: unknown coverage level \"RESULT_ONLY\""]
        },
        {
            "scorer pad buttons",
            root => root["scorer_pad"]!["groups"]![0]!["buttons"]![0] = "GOALL",
            ["football.yaml: $.scorer_pad.groups[0].buttons[0]: unknown event type \"GOALL\""]
        },
        {
            "scorer pad coverage",
            root => WithoutResultOnly(root, root => root["scorer_pad"]!["groups"]![4]!["coverage"] = "RESULT_ONLY"),
            ["football.yaml: $.scorer_pad.groups[4].coverage: unknown coverage level \"RESULT_ONLY\""]
        },
        {
            "follow-up events",
            root => Event(root, "PENALTY_MISSED")["follow_up"] = new JsonObject { ["requires_preceding"] = new JsonArray("PENALTY_AWARDED") },
            ["football.yaml: $.event_types[3].follow_up.requires_preceding[0]: unknown event type \"PENALTY_AWARDED\""]
        },
        {
            "reversible_by events",
            root => Event(root, "GOAL")["reversible_by"]![0] = "VAR_DECISIONS",
            ["football.yaml: $.event_types[0].reversible_by[0]: unknown event type \"VAR_DECISIONS\""]
        },
        {
            "tie resolutions the family knows",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("SUPER_OVER"),
            ["football.yaml: $.supports.tie_resolutions[3]: unknown tie resolution \"SUPER_OVER\" for scoring family ADDITIVE"]
        },
        {
            "a shoot-out tie resolution needs a SHOOTOUT period",
            root =>
            {
                RemovePeriods(root, "SHOOTOUT");
                foreach (var item in root["event_types"]!.AsArray())
                {
                    var phases = item!["phases"]!.AsArray();
                    phases.Remove(phases.SingleOrDefault(phase => phase!.GetValue<string>() == "SHOOTOUT"));
                    if (phases.Count == 0)
                    {
                        phases.Add("PLAY");
                    }
                }

                root["scorer_pad"]!["groups"]![3]!.AsObject().Remove("phases");
            },
            [
                "football.yaml: $.supports.tie_resolutions[1]: tie resolution \"EXTRA_PERIODS_THEN_SHOOTOUT\" needs a period of kind SHOOTOUT",
                "football.yaml: $.supports.tie_resolutions[2]: tie resolution \"SHOOTOUT\" needs a period of kind SHOOTOUT",
            ]
        },
        {
            "an extra-periods tie resolution needs an overtime PLAY period",
            root =>
            {
                foreach (var period in root["periods"]!.AsArray())
                {
                    period!.AsObject().Remove("overtime");
                }
            },
            ["football.yaml: $.supports.tie_resolutions[1]: tie resolution \"EXTRA_PERIODS_THEN_SHOOTOUT\" needs an overtime PLAY period"]
        },
        {
            "a sudden-death tie resolution needs a sudden_death period",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("EXTRA_PERIODS_SUDDEN_DEATH"),
            ["football.yaml: $.supports.tie_resolutions[3]: tie resolution \"EXTRA_PERIODS_SUDDEN_DEATH\" needs a sudden_death overtime PLAY period"]
        },
        {
            "a sudden_death break doesn't count",
            root =>
            {
                root["supports"]!["tie_resolutions"]!.AsArray().Add("EXTRA_PERIODS_SUDDEN_DEATH");
                Period(root, "EXTRA_TIME_HALF_TIME")["sudden_death"] = true;
            },
            ["football.yaml: $.supports.tie_resolutions[3]: tie resolution \"EXTRA_PERIODS_SUDDEN_DEATH\" needs a sudden_death overtime PLAY period"]
        },
        {
            "a repeated extra period needs a repeatable overtime PLAY period",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("REPEAT_EXTRA_PERIOD"),
            ["football.yaml: $.supports.tie_resolutions[3]: tie resolution \"REPEAT_EXTRA_PERIOD\" needs a repeatable overtime PLAY period"]
        },
        {
            "score components the family knows",
            root => Event(root, "SHOOTOUT_GOAL")["component"] = "penalties",
            ["football.yaml: $.event_types[4].component: unknown score component \"penalties\" for scoring family ADDITIVE"]
        },
        {
            "a scoring family with an engine",
            root => root["scoring_family"] = "SETS",
            ["football.yaml: $.scoring_family: scoring family \"SETS\" has no engine yet"]
        },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_rule_names_the_file_the_path_and_the_problem(string rule, Action<JsonObject> edit, string[] expected)
    {
        Validate(edit).Select(p => p.ToString()).ShouldBe(expected, rule);
    }

    private static IReadOnlyList<DefinitionProblem> Validate(Action<JsonObject> edit)
    {
        var root = YamlJson.Parse(_files.Read(_file))!.AsObject();
        edit(root);
        return SportDefinitionValidator.Validate(_file, root.ToJsonString());
    }

    private static JsonObject Event(JsonObject root, string code) => Find(root, "event_types", code);

    private static JsonObject Statistic(JsonObject root, string code) => Find(root, "statistics", code);

    private static JsonObject Period(JsonObject root, string code) => Find(root, "periods", code);

    private static JsonObject Find(JsonObject root, string list, string code) =>
        root[list]!.AsArray().Single(item => item!["code"]!.GetValue<string>() == code)!.AsObject();

    private static JsonObject Ratio(string numerator, string denominator) =>
        new() { ["builtin"] = "RATIO", ["numerator"] = numerator, ["denominator"] = denominator };

    private static void RemovePeriods(JsonObject root, string kind)
    {
        var periods = root["periods"]!.AsArray();
        foreach (var period in periods.Where(p => p!["kind"]!.GetValue<string>() == kind).ToList())
        {
            periods.Remove(period);
        }
    }

    // Football uses RESULT_ONLY nowhere else, so dropping it from the definition's levels and using it once gives one problem.
    private static void WithoutResultOnly(JsonObject root, Action<JsonObject> use)
    {
        root["coverage_levels"]!.AsArray().RemoveAt(0);
        use(root);
    }
}
