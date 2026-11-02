using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// Each rejecting case edits a copy of an embedded template. JSON is YAML, so the edited tree goes back through the
// validator as text, like a file would. Each case lists every problem its edit causes.
public sealed class RulesetTemplateValidatorTests
{
    private const string _league = "football-league.yaml";

    private const string _cup = "football-cup.yaml";

    private static readonly RulesetTemplateFiles _files = new();

    private static readonly RulesetReferences _references =
        RulesetReferences.FromFiles(new SportDefinitionFiles(), new FormatDefinitionFiles(), new TiebreakerCatalogue());

    [Fact]
    public void The_embedded_templates_are_valid()
    {
        _files.Names.ShouldBe([_cup, _league]);
        foreach (var file in _files.Names)
        {
            RulesetTemplateValidator.Validate(file, _files.Read(file), _references).ShouldBeEmpty(file);
        }
    }

    [Fact]
    public void The_tiebreaker_catalogue_is_valid_and_not_a_template()
    {
        var catalogue = new TiebreakerCatalogue();

        catalogue.Validate().ShouldBeEmpty();
        catalogue.All.Select(tiebreaker => tiebreaker.Code).ShouldBe(
        [
            "POINTS", "WINS", "GOAL_DIFFERENCE", "DIFFERENCE", "GOALS_FOR", "SCORED", "TRIES_SCORED", "TRY_DIFFERENCE",
            "NET_RUN_RATE", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS",
        ]);
        catalogue.All.Single(tiebreaker => tiebreaker.Code == "TRIES_SCORED").Sports.ShouldBe(["RUGBY_UNION"]);
        catalogue.All.Single(tiebreaker => tiebreaker.Code == "POINTS").AppliesTo("FOOTBALL").ShouldBeTrue();
        _files.Names.ShouldNotContain(TiebreakerCatalogue.File);
    }

    // A misspelt sport would make a tiebreaker apply to none (review #84 R4): each is the `sport` of a reference definition.
    [Fact]
    public void The_catalogues_sport_limits_name_sports_of_the_reference_definitions()
    {
        var docs = Path.Combine(ReferenceSportFilesTests.RepositoryRoot(), "docs", "domain", "sports");
        var sports = Directory.GetFiles(docs, "*.yaml")
            .Select(file => YamlJson.Parse(File.ReadAllText(file))!["sport"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        var limits = new TiebreakerCatalogue().All.SelectMany(tiebreaker => tiebreaker.Sports).ToList();

        limits.ShouldNotBeEmpty();
        limits.ShouldAllBe(sport => sports.Contains(sport));
    }

    [Fact]
    public void A_duplicate_tiebreaker_is_a_problem()
    {
        var catalogue = new EditedCatalogue(yaml => yaml.Replace("code: WINS,", "code: POINTS,", StringComparison.Ordinal));

        catalogue.Validate().Select(p => p.ToString()).ShouldBe(["tiebreakers.yaml: $.tiebreakers[1].code: duplicate tiebreaker code \"POINTS\""]);
        Should.Throw<InvalidOperationException>(() => catalogue.All).Message.ShouldStartWith("The tiebreaker catalogue is invalid:");
    }

    [Fact]
    public void Invalid_YAML_is_one_problem_at_the_root()
    {
        var problem = RulesetTemplateValidator.Validate(_league, "code: [FOOTBALL_LEAGUE", _references).ShouldHaveSingleItem();

        problem.ToString().ShouldStartWith("football-league.yaml: $: not valid YAML (line ");
    }

    public static TheoryData<string, Action<JsonObject>, string[]> Shapes => new()
    {
        {
            "a missing member",
            root => root.Remove("formats"),
            ["football-league.yaml: $: Required properties [\"formats\"] are not present"]
        },
        {
            "an unknown member of the values",
            root => Values(root)["standings"]!["bonus"] = 1,
            ["football-league.yaml: $.values.standings.bonus: unknown member"]
        },
        {
            "a missing section of the values",
            root => Values(root).Remove("eligibility"),
            ["football-league.yaml: $.values: Required properties [\"eligibility\"] are not present"]
        },
        {
            "a negative count",
            root => Values(root)["substitutions"]!["max"] = -1,
            ["football-league.yaml: $.values.substitutions.max: -1 should be at least 0"]
        },
        {
            "an unknown lock policy",
            root => Values(root)["roster"]!["lock_policy"] = "NEVER",
            ["football-league.yaml: $.values.roster.lock_policy: Value should match one of the values specified by the enum"]
        },
        {
            "an editable path that isn't dotted",
            root => Editable(root)["roster"] = new JsonObject { ["type"] = "bool", ["label"] = "Roster" },
            ["football-league.yaml: $.editable.roster: The string value is not a match for the indicated regular expression"]
        },
        {
            "a number parameter without bounds",
            root => Parameter(root, "roster.min").Remove("max"),
            ["football-league.yaml: $.editable.roster.min: Required properties [\"max\"] are not present"]
        },
        {
            "a list parameter without values",
            root => Parameter(root, "standings.tiebreakers").Remove("values"),
            ["football-league.yaml: $.editable.standings.tiebreakers: Required properties [\"values\"] are not present"]
        },
        {
            "a default in the parameter (it comes from the values)",
            root => Parameter(root, "roster.min")["default"] = 18,
            ["football-league.yaml: $.editable.roster.min.default: unknown member"]
        },
        {
            "a tiebreaker listed twice (the schema; the rules refuse a tenant's repeat)",
            root => Values(root)["standings"]!["tiebreakers"]!.AsArray().Add("POINTS"),
            ["football-league.yaml: $.values.standings.tiebreakers: Found duplicates at the following index pairs: [\"(0,7)\"]"]
        },
        {
            "a date that isn't a date",
            root => Values(root)["eligibility"]!["age_reference_date"] = "1 January 2027",
            ["football-league.yaml: $.values.eligibility.age_reference_date: The string value is not a match for the indicated regular expression"]
        },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Shape_problems_name_the_file_the_path_and_the_problem(string shape, Action<JsonObject> edit, string[] expected)
    {
        Validate(_league, edit).Select(p => p.ToString()).ShouldBe(expected, shape);
    }

    public static TheoryData<string, string, Action<JsonObject>, string[]> Rules => new()
    {
        {
            "the sport definition exists at the pinned version", _league,
            root => root["sport"]!["version"] = 2,
            ["football-league.yaml: $.sport: no sport definition FOOTBALL_ASSOCIATION@2"]
        },
        {
            "every format exists", _league,
            root => root["formats"]!.AsArray().Add("SWISS"),
            ["football-league.yaml: $.formats[2]: unknown format \"SWISS\""]
        },
        {
            "an editable parameter names a value", _league,
            root => Editable(root)["standings.points.bonus"] = new JsonObject { ["type"] = "int", ["label"] = "Bonus", ["min"] = 0, ["max"] = 1 },
            ["football-league.yaml: $.editable.standings.points.bonus: no value at values.standings.points.bonus"]
        },
        {
            "an editable parameter has the value's type", _league,
            root => Editable(root)["roster.min"] = new JsonObject { ["type"] = "enum", ["label"] = "Smallest squad", ["values"] = new JsonArray("SMALL") },
            ["football-league.yaml: $.editable.roster.min: the value at values.roster.min is not an enum"]
        },
        {
            "a null value isn't editable", _league,
            root => Editable(root)["eligibility.min_age"] = new JsonObject { ["type"] = "int", ["label"] = "Youngest", ["min"] = 0, ["max"] = 99 },
            ["football-league.yaml: $.editable.eligibility.min_age: the value at values.eligibility.min_age is not an int"]
        },
        {
            "a number's bounds are in order", _league,
            root => Parameter(root, "roster.min")["min"] = 50,
            ["football-league.yaml: $.editable.roster.min.max: 40 is below min (50)"]
        },
        {
            "a number's value is within its bounds", _league,
            root => Parameter(root, "standings.points.win")["min"] = 4,
            ["football-league.yaml: $.editable.standings.points.win: the value at values.standings.points.win (3) is outside 4..5"]
        },
        {
            "a list's value holds only its values", _league,
            root => Parameter(root, "standings.tiebreakers")["values"] = new JsonArray("POINTS", "GOAL_DIFFERENCE", "GOALS_FOR", "H2H_POINTS", "H2H_DIFFERENCE", "DRAWING_OF_LOTS"),
            ["football-league.yaml: $.editable.standings.tiebreakers: the value at values.standings.tiebreakers has \"H2H_SCORED\", which is not one of the values"]
        },
        {
            "a choice's value is one of its values", _cup,
            root => Editable(root)["contest.tie_resolution"] = new JsonObject { ["type"] = "enum", ["label"] = "Level score", ["values"] = new JsonArray("SHOOTOUT") },
            [
                "football-cup.yaml: $.editable.contest.tie_resolution: the value at values.contest.tie_resolution has \"EXTRA_PERIODS_THEN_SHOOTOUT\", which is not one of the values",
                "football-cup.yaml: $.editable.contest.tie_resolution.values: \"SHOOTOUT\" doesn't fit the template's extra periods: tie resolution SHOOTOUT plays no extra time before the shoot-out",
            ]
        },
        {
            "a tie resolution a tenant may pick is one the sport supports", _cup,
            root => Editable(root)["contest.tie_resolution"] = new JsonObject
            {
                ["type"] = "enum", ["label"] = "Level score", ["values"] = new JsonArray("EXTRA_PERIODS_THEN_SHOOTOUT", "EXTRA_PERIODS_SUDDEN_DEATH"),
            },
            ["football-cup.yaml: $.editable.contest.tie_resolution.values: the sport doesn't support \"EXTRA_PERIODS_SUDDEN_DEATH\""]
        },
        {
            "a tie resolution a tenant may pick fits the template's extra periods (they can't change them)", _cup,
            root => Editable(root)["contest.tie_resolution"] = new JsonObject
            {
                ["type"] = "enum", ["label"] = "Level score", ["values"] = new JsonArray("EXTRA_PERIODS_THEN_SHOOTOUT", "DRAW"),
            },
            ["football-cup.yaml: $.editable.contest.tie_resolution.values: \"DRAW\" doesn't fit the template's extra periods: tie resolution DRAW plays no extra periods"]
        },
        {
            "a tiebreaker a tenant may pick is in the catalogue and applies to the sport", _league,
            root => Parameter(root, "standings.tiebreakers")["values"]!.AsArray().Add("TRIES_SCORED"),
            ["football-league.yaml: $.editable.standings.tiebreakers.values: tiebreaker \"TRIES_SCORED\" doesn't apply to Football"]
        },
        {
            "only paths whose codes are checked may be choices or lists", _league,
            root => Editable(root)["operations.readiness"] = new JsonObject { ["type"] = "list", ["label"] = "Readiness", ["values"] = new JsonArray("LINEUPS_APPROVED", "OFFICIALS_MINIMUM", "VENUE_SET") },
            ["football-league.yaml: $.editable.operations.readiness: values.operations.readiness can't be a tenant's choice: only standings.tiebreakers, contest.tie_resolution and contest.coverage_default are"]
        },
        {
            "a coverage level a tenant may pick is one the sport has", _league,
            root => Editable(root)["contest.coverage_default"] = new JsonObject { ["type"] = "enum", ["label"] = "Coverage", ["values"] = new JsonArray("STANDARD", "PLAY_BY_PLAY") },
            ["football-league.yaml: $.editable.contest.coverage_default.values: the sport has no PLAY_BY_PLAY coverage"]
        },
        {
            "the periods are the definition's", _league,
            root => Values(root)["contest"]!["period_seconds"]!["THIRD_HALF"] = 600,
            ["football-league.yaml: $.values.contest.period_seconds.THIRD_HALF: unknown period \"THIRD_HALF\""]
        },
        {
            "the tie resolution is one the sport supports", _league,
            root => Values(root)["contest"]!["tie_resolution"] = "REPEAT_EXTRA_PERIOD",
            ["football-league.yaml: $.values.contest.tie_resolution: the sport doesn't support \"REPEAT_EXTRA_PERIOD\""]
        },
        {
            "a draw plays no extra periods", _league,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("PENALTY_SHOOTOUT"),
            ["football-league.yaml: $.values.contest.extra_period_sequence: tie resolution DRAW plays no extra periods"]
        },
        {
            "any other tie resolution needs its extra periods", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray(),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: tie resolution EXTRA_PERIODS_THEN_SHOOTOUT needs its extra periods"]
        },
        {
            "a shoot-out needs a shoot-out period", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("AWAITING_EXTRA_TIME", "EXTRA_TIME_FIRST_HALF", "EXTRA_TIME_HALF_TIME", "EXTRA_TIME_SECOND_HALF"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: tie resolution EXTRA_PERIODS_THEN_SHOOTOUT needs a shoot-out period"]
        },
        {
            "the extra periods are the definition's", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"]!.AsArray().Insert(2, "GOLDEN_GOAL"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: unknown period \"GOLDEN_GOAL\" at 2"]
        },
        {
            "an extra period is listed once", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"]!.AsArray().Add("PENALTY_SHOOTOUT"),
            [
                "football-cup.yaml: $.values.contest.extra_period_sequence: period \"PENALTY_SHOOTOUT\" is listed twice",
                "football-cup.yaml: $.values.contest.extra_period_sequence: only one shoot-out period",
            ]
        },
        {
            "an extra period is an overtime period or a break before one", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("FIRST_HALF", "AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: period \"FIRST_HALF\" at 0 is neither an overtime period nor the break that opens them"]
        },
        {
            "half-time doesn't open the extra periods (the definition lists it before the second half)", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("HALF_TIME", "EXTRA_TIME_FIRST_HALF", "AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: period \"HALF_TIME\" at 0 is neither an overtime period nor the break that opens them"]
        },
        {
            "the break that opens the extra periods comes first", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("EXTRA_TIME_FIRST_HALF", "AWAITING_EXTRA_TIME", "EXTRA_TIME_SECOND_HALF", "AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: period \"AWAITING_EXTRA_TIME\" at 1 is neither an overtime period nor the break that opens them"]
        },
        {
            "a broken sequence is reported on the values, not again on each tie resolution offered", _cup,
            root =>
            {
                Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("AWAITING_EXTRA_TIME", "EXTRA_TIME_FIRST_HALF", "EXTRA_TIME_HALF_TIME", "EXTRA_TIME_SECOND_HALF", "PENALTY_SHOOTOUT", "AWAITING_SHOOTOUT");
                Editable(root)["contest.tie_resolution"] = new JsonObject
                {
                    ["type"] = "enum", ["label"] = "Level score", ["values"] = new JsonArray("EXTRA_PERIODS_THEN_SHOOTOUT"),
                };
            },
            ["football-cup.yaml: $.values.contest.extra_period_sequence: the shoot-out period \"PENALTY_SHOOTOUT\" must come last"]
        },
        {
            "the shoot-out comes last", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("AWAITING_EXTRA_TIME", "EXTRA_TIME_FIRST_HALF", "EXTRA_TIME_HALF_TIME", "EXTRA_TIME_SECOND_HALF", "PENALTY_SHOOTOUT", "AWAITING_SHOOTOUT"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: the shoot-out period \"PENALTY_SHOOTOUT\" must come last"]
        },
        {
            "extra periods then a shoot-out needs extra time", _cup,
            root => Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT"),
            ["football-cup.yaml: $.values.contest.extra_period_sequence: tie resolution EXTRA_PERIODS_THEN_SHOOTOUT needs an extra period of play"]
        },
        {
            "a shoot-out alone plays no extra time", _cup,
            root => Values(root)["contest"]!["tie_resolution"] = "SHOOTOUT",
            ["football-cup.yaml: $.values.contest.extra_period_sequence: tie resolution SHOOTOUT plays no extra time before the shoot-out"]
        },
        {
            "a shoot-out alone is accepted with its own periods", _cup,
            root =>
            {
                Values(root)["contest"]!["tie_resolution"] = "SHOOTOUT";
                Values(root)["contest"]!["extra_period_sequence"] = new JsonArray("AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT");
            },
            []
        },
        {
            "an unsupported tie resolution still has its extra periods checked", _cup,
            root =>
            {
                Values(root)["contest"]!["tie_resolution"] = "REPEAT_EXTRA_PERIOD";
                Values(root)["contest"]!["extra_period_sequence"]!.AsArray().Insert(2, "GOLDEN_GOAL");
            },
            [
                "football-cup.yaml: $.values.contest.extra_period_sequence: unknown period \"GOLDEN_GOAL\" at 2",
                "football-cup.yaml: $.values.contest.tie_resolution: the sport doesn't support \"REPEAT_EXTRA_PERIOD\"",
            ]
        },
        {
            "a position rule names a position or a group", _league,
            root => Values(root)["lineup"]!["positions"]!["LIBERO"] = new JsonObject { ["min"] = 0, ["max"] = 1 },
            ["football-league.yaml: $.values.lineup.positions.LIBERO: unknown position or position group \"LIBERO\""]
        },
        {
            "a position rule's range is in order", _league,
            root => Values(root)["lineup"]!["positions"]!["DEFENDER"] = new JsonObject { ["min"] = 4, ["max"] = 3 },
            ["football-league.yaml: $.values.lineup.positions.DEFENDER.max: 3 is below min (4)"]
        },
        {
            "a position rule fits the starters", _league,
            root => Values(root)["lineup"]!["positions"]!["DEFENDER"] = new JsonObject { ["min"] = 12, ["max"] = 12 },
            ["football-league.yaml: $.values.lineup.positions.DEFENDER.min: 12 is more than the 11 starters"]
        },
        {
            "the roster's range is in order", _league,
            root => Values(root)["roster"]!["min"] = 31,
            ["football-league.yaml: $.values.roster.max: 30 is below roster.min (31)"]
        },
        {
            "the smallest squad has the starters", _league,
            root => Values(root)["lineup"]!["starters"] = 19,
            ["football-league.yaml: $.values.roster.min: 18 is fewer than the 19 starters"]
        },
        {
            "the largest squad has the starters and the bench", _league,
            root => Values(root)["roster"]!["max"] = 19,
            ["football-league.yaml: $.values.roster.max: 19 is fewer than the 11 starters and 9 on the bench"]
        },
        {
            "the officials are the definition's", _league,
            root => Values(root)["operations"]!["officials_minimum"]!["UMPIRE"] = 1,
            ["football-league.yaml: $.values.operations.officials_minimum.UMPIRE: unknown official role \"UMPIRE\""]
        },
        {
            "every tiebreaker is in the catalogue", _league,
            root => Values(root)["standings"]!["tiebreakers"]!.AsArray().Add("FAIR_PLAY"),
            [
                "football-league.yaml: $.editable.standings.tiebreakers: the value at values.standings.tiebreakers has \"FAIR_PLAY\", which is not one of the values",
                "football-league.yaml: $.values.standings.tiebreakers: unknown tiebreaker \"FAIR_PLAY\"",
            ]
        },
        {
            "every tiebreaker applies to the sport", _league,
            root => Values(root)["standings"]!["tiebreakers"]!.AsArray().Add("NET_RUN_RATE"),
            [
                "football-league.yaml: $.editable.standings.tiebreakers: the value at values.standings.tiebreakers has \"NET_RUN_RATE\", which is not one of the values",
                "football-league.yaml: $.values.standings.tiebreakers: tiebreaker \"NET_RUN_RATE\" doesn't apply to Football",
            ]
        },
        {
            "the age range is in order", _league,
            root =>
            {
                Values(root)["eligibility"]!["min_age"] = 18;
                Values(root)["eligibility"]!["max_age"] = 16;
            },
            ["football-league.yaml: $.values.eligibility.max_age: 16 is below min_age (18)"]
        },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_rule_names_the_file_the_path_and_the_problem(string rule, string file, Action<JsonObject> edit, string[] expected)
    {
        Validate(file, edit).Select(p => p.ToString()).ShouldBe(expected, rule);
    }

    // The edited sport definition is the only one the template can find.
    [Fact]
    public void Every_format_takes_the_sports_contest_shape()
    {
        var problems = ValidateWithFootball(_league, sport => sport.Replace("shape: HEAD_TO_HEAD", "shape: FIELD", StringComparison.Ordinal));

        problems.ShouldContain("football-league.yaml: $.formats[0]: format ROUND_ROBIN doesn't take FIELD contests");
        problems.ShouldContain("football-league.yaml: $.formats[1]: format SINGLE_CONTEST doesn't take FIELD contests");
    }

    [Fact]
    public void The_coverage_is_one_the_sport_has()
    {
        var problems = ValidateWithFootball(_league, sport => sport.Replace(
            "coverage_levels: [RESULT_ONLY, BASIC, STANDARD, DETAILED]", "coverage_levels: [RESULT_ONLY, BASIC]", StringComparison.Ordinal));

        problems.ShouldBe(["football-league.yaml: $.values.contest.coverage_default: the sport has no STANDARD coverage"]);
    }

    private static IReadOnlyList<DefinitionProblem> Validate(string file, Action<JsonObject> edit)
    {
        var root = YamlJson.Parse(_files.Read(file))!.AsObject();
        edit(root);
        return RulesetTemplateValidator.Validate(file, root.ToJsonString(), _references);
    }

    private static List<string> ValidateWithFootball(string file, Func<string, string> editSport)
    {
        var sports = new SportDefinitionFiles();
        var formats = new FormatDefinitionFiles();
        var references = new RulesetReferences(
            [SportDefinitionContent.Read(YamlJson.Parse(editSport(sports.Read("football.yaml")))!)],
            formats.Names.Select(name => FormatDefinitionContent.Read(YamlJson.Parse(formats.Read(name))!)),
            new TiebreakerCatalogue().All);
        return [.. RulesetTemplateValidator.Validate(file, _files.Read(file), references).Select(p => p.ToString())];
    }

    private static JsonObject Values(JsonObject root) => root["values"]!.AsObject();

    private static JsonObject Editable(JsonObject root) => root["editable"]!.AsObject();

    private static JsonObject Parameter(JsonObject root, string path) => Editable(root)[path]!.AsObject();

    private sealed class EditedCatalogue(Func<string, string> edit) : TiebreakerCatalogue
    {
        public override string Text() => edit(base.Text());
    }
}
