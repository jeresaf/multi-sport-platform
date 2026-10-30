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
    public void A_missing_member_is_a_shape_problem()
    {
        var problems = Validate(root => root.Remove("scoring_family"));

        problems.ShouldHaveSingleItem().ToString().ShouldBe("football.yaml: $: Required properties [\"scoring_family\"] are not present");
    }

    [Fact]
    public void An_unknown_member_is_a_shape_problem_at_its_path()
    {
        var problems = Validate(root => Event(root, "GOAL")["pointz"] = 1);

        problems.ShouldHaveSingleItem().ToString().ShouldBe("football.yaml: $.event_types[0].pointz: unknown member");
    }

    [Fact]
    public void A_wrongly_typed_value_is_a_shape_problem_at_its_path()
    {
        var problems = Validate(root => root["periods"]![0]!["default_seconds"] = "45 minutes");

        problems.ShouldContain(p => p.Path == "$.periods[0].default_seconds");
    }

    [Fact]
    public void Semantic_rules_run_only_on_a_valid_shape()
    {
        var problems = Validate(root =>
        {
            root.Remove("name");
            Event(root, "GOAL")["actors"]![0]!["role"] = "SCORERR";
        });

        problems.ShouldAllBe(p => p.Path == "$");
    }

    public static TheoryData<string, Action<JsonObject>, string> Rules => new()
    {
        {
            "unique codes",
            root => root["positions"]!.AsArray().Add(new JsonObject { ["code"] = "GK", ["label"] = "Keeper", ["group"] = "GOALKEEPER" }),
            "football.yaml: $.positions[12].code: duplicate code \"GK\" (also at $.positions[0])"
        },
        {
            "event codes don't redefine generic event types",
            root => Event(root, "CORNER")["code"] = "NOTE",
            "football.yaml: $.event_types[15].code: \"NOTE\" is a generic event type and can't be redefined"
        },
        {
            "event actor role",
            root => Event(root, "GOAL")["actors"]![0]!["role"] = "SCORERR",
            "football.yaml: $.event_types[0].actors[0].role: unknown actor role \"SCORERR\""
        },
        {
            "event distinct_from is a role on the same event",
            root => Event(root, "GOAL")["actors"]![1]!["distinct_from"] = "TAKER",
            "football.yaml: $.event_types[0].actors[1].distinct_from: \"TAKER\" is not a role on this event"
        },
        {
            "event actor required_from is one of the definition's coverage levels",
            root => WithoutCoverage(root, "STANDARD", item => item["coverage"] = "BASIC"),
            "football.yaml: $.event_types[0].actors[0].required_from: unknown coverage level \"STANDARD\""
        },
        {
            "event coverage is one of the definition's coverage levels",
            root => WithoutCoverage(root, "DETAILED", _ => { }),
            "football.yaml: $.event_types[13].coverage: unknown coverage level \"DETAILED\""
        },
        {
            "event phases are kinds some period has",
            root => RemovePeriods(root, "SHOOTOUT"),
            "football.yaml: $.event_types[4].phases[0]: no period has kind \"SHOOTOUT\""
        },
        {
            "position constraint group",
            root => root["lineup"]!["position_constraints"]![0]!["group"] = "KEEPER",
            "football.yaml: $.lineup.position_constraints[0].group: unknown position group \"KEEPER\""
        },
        {
            "statistic events",
            root => Statistic(root, "GOALS")["derive"]!["count_events"]![1] = "PENALTY_GOALS",
            "football.yaml: $.statistics[0].derive.count_events[1]: unknown event type \"PENALTY_GOALS\""
        },
        {
            "statistic actor roles",
            root => Statistic(root, "GOALS")["derive"]!["actor_roles"]![0] = "STRIKER",
            "football.yaml: $.statistics[0].derive.actor_roles[0]: unknown actor role \"STRIKER\""
        },
        {
            "statistic ratio parts",
            root => Statistic(root, "POSSESSION_PERCENT")["derive"] = new JsonObject
            {
                ["builtin"] = "RATIO", ["numerator"] = "SHOTS_ON_TARGET", ["denominator"] = "SHOTS_TAKEN",
            },
            "football.yaml: $.statistics[16].derive.denominator: unknown statistic \"SHOTS_TAKEN\""
        },
        {
            "statistic coverage is one of the definition's coverage levels",
            root =>
            {
                root["coverage_levels"]!.AsArray().RemoveAt(0);
                Statistic(root, "GOALS")["coverage"] = "RESULT_ONLY";
            },
            "football.yaml: $.statistics[0].coverage: unknown coverage level \"RESULT_ONLY\""
        },
        {
            "scorer pad buttons",
            root => root["scorer_pad"]!["groups"]![0]!["buttons"]![0] = "GOALL",
            "football.yaml: $.scorer_pad.groups[0].buttons[0]: unknown event type \"GOALL\""
        },
        {
            "scorer pad coverage",
            root => WithoutCoverage(root, "DETAILED", item => item["coverage"] = "STANDARD", keepPadCoverage: true),
            "football.yaml: $.scorer_pad.groups[4].coverage: unknown coverage level \"DETAILED\""
        },
        {
            "follow-up events",
            root => Event(root, "PENALTY_MISSED")["follow_up"] = new JsonObject { ["requires_preceding"] = new JsonArray("PENALTY_AWARDED") },
            "football.yaml: $.event_types[3].follow_up.requires_preceding[0]: unknown event type \"PENALTY_AWARDED\""
        },
        {
            "reversible_by events",
            root => Event(root, "GOAL")["reversible_by"]![0] = "VAR_DECISIONS",
            "football.yaml: $.event_types[0].reversible_by[0]: unknown event type \"VAR_DECISIONS\""
        },
        {
            "tie resolutions the family knows",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("SUPER_OVER"),
            "football.yaml: $.supports.tie_resolutions[3]: unknown tie resolution \"SUPER_OVER\" for scoring family ADDITIVE"
        },
        {
            "a shoot-out tie resolution needs a SHOOTOUT period",
            root =>
            {
                RemovePeriods(root, "SHOOTOUT");
                Event(root, "SHOOTOUT_GOAL")["phases"] = new JsonArray("PLAY");
                Event(root, "SHOOTOUT_MISS")["phases"] = new JsonArray("PLAY");

                root["scorer_pad"]!["groups"]![3]!.AsObject().Remove("phases");
            },
            "football.yaml: $.supports.tie_resolutions[1]: tie resolution \"EXTRA_PERIODS_THEN_SHOOTOUT\" needs a period of kind SHOOTOUT"
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
            "football.yaml: $.supports.tie_resolutions[1]: tie resolution \"EXTRA_PERIODS_THEN_SHOOTOUT\" needs an overtime PLAY period"
        },
        {
            "a sudden-death tie resolution needs a sudden_death period",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("EXTRA_PERIODS_SUDDEN_DEATH"),
            "football.yaml: $.supports.tie_resolutions[3]: tie resolution \"EXTRA_PERIODS_SUDDEN_DEATH\" needs a sudden_death period"
        },
        {
            "a repeated extra period needs a repeatable overtime PLAY period",
            root => root["supports"]!["tie_resolutions"]!.AsArray().Add("REPEAT_EXTRA_PERIOD"),
            "football.yaml: $.supports.tie_resolutions[3]: tie resolution \"REPEAT_EXTRA_PERIOD\" needs a repeatable overtime PLAY period"
        },
        {
            "score components the family knows",
            root => Event(root, "SHOOTOUT_GOAL")["component"] = "penalties",
            "football.yaml: $.event_types[4].component: unknown score component \"penalties\" for scoring family ADDITIVE"
        },
        {
            "a scoring family with an engine",
            root => root["scoring_family"] = "SETS",
            "football.yaml: $.scoring_family: scoring family \"SETS\" has no engine yet"
        },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_rule_names_the_file_the_path_and_the_problem(string rule, Action<JsonObject> edit, string expected)
    {
        var problems = Validate(edit);

        problems.Select(p => p.ToString()).ShouldContain(expected, rule);
    }

    private static IReadOnlyList<DefinitionProblem> Validate(Action<JsonObject> edit)
    {
        var root = YamlJson.Parse(_files.Read(_file))!.AsObject();
        edit(root);
        return SportDefinitionValidator.Validate(_file, root.ToJsonString());
    }

    private static JsonObject Event(JsonObject root, string code) => Find(root, "event_types", code);

    private static JsonObject Statistic(JsonObject root, string code) => Find(root, "statistics", code);

    private static JsonObject Find(JsonObject root, string list, string code) =>
        root[list]!.AsArray().Single(item => item!["code"]!.GetValue<string>() == code)!.AsObject();

    private static void RemovePeriods(JsonObject root, string kind)
    {
        var periods = root["periods"]!.AsArray();
        foreach (var period in periods.Where(p => p!["kind"]!.GetValue<string>() == kind).ToList())
        {
            periods.Remove(period);
        }
    }

    // Drops a coverage level from the definition; events at that level are moved by `move` so only the rule under test fails.
    private static void WithoutCoverage(JsonObject root, string level, Action<JsonObject> move, bool keepPadCoverage = false)
    {
        var levels = root["coverage_levels"]!.AsArray();
        levels.Remove(levels.Single(l => l!.GetValue<string>() == level));
        foreach (var item in root["event_types"]!.AsArray().Select(e => e!.AsObject()))
        {
            if (item["coverage"]!.GetValue<string>() == level)
            {
                move(item);
            }
        }

        foreach (var statistic in root["statistics"]!.AsArray())
        {
            if (statistic!["coverage"]!.GetValue<string>() == level)
            {
                statistic["coverage"] = "BASIC";
            }
        }

        if (!keepPadCoverage)
        {
            foreach (var group in root["scorer_pad"]!["groups"]!.AsArray())
            {
                group!.AsObject().Remove("coverage");
            }
        }
    }
}
