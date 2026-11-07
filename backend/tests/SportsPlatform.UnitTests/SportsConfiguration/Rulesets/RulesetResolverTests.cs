using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;
using SportsPlatform.UnitTests.SportsConfiguration.Generators;
using static SportsPlatform.UnitTests.SportsConfiguration.Rulesets.RulesetTestData;

namespace SportsPlatform.UnitTests.SportsConfiguration.Rulesets;

// S05-T4's acceptance: the same inputs give the same snapshot hash, and every invalid combination is refused with its
// code, naming the field (the preview's: overrides.<path>, format.code, format.version, format.parameters.<code>).
public sealed class RulesetResolverTests
{
    private const string _win = "standings.points.win";

    private const string _tiebreakers = "standings.tiebreakers";

    // The slice demo: KMSA's league with 4 points for a win, drawn home and away. Pinned, so any change to how a
    // snapshot is written (or to the files it reads) shows here first.
    private const string _demoHash = "sha256:b198a41e3e0eb951c898f1f09fd67800c78286a6f2a8b4d574f7efe7891c0181";

    private static readonly RulesetResolver _resolver = Resolver();

    [Fact]
    public async Task The_league_with_4_points_for_a_win_resolves_to_its_pinned_hash()
    {
        var snapshot = await _resolver.ResolveAsync(Request(parameters: [("cycles", ParameterValue.Of(2))], overrides: (_win, ParameterValue.Of(4))));

        snapshot.Hash.ShouldBe(_demoHash);
        snapshot.Values.Standings.Points.Win.ShouldBe(4);
        snapshot.Sport.ShouldBe(new RulesSnapshotSport("FOOTBALL_ASSOCIATION", 1, Football.ContentHash));
        snapshot.Template.ShouldBe(new RulesSnapshotTemplate("FOOTBALL_LEAGUE", 1));
        (snapshot.Format.Code, snapshot.Format.Version).ShouldBe(("ROUND_ROBIN", 1));
        snapshot.Format.Parameters.ShouldBe(new Dictionary<string, ParameterValue>
        {
            ["cycles"] = ParameterValue.Of(2),
            ["home_away_balance"] = ParameterValue.Of(true),
            ["seeding"] = ParameterValue.Of("RANDOM"),
        });
    }

    [Fact]
    public async Task The_same_inputs_give_the_same_snapshot_and_hash()
    {
        var first = await _resolver.ResolveAsync(Request(overrides: [(_win, ParameterValue.Of(4)), ("roster.max", ParameterValue.Of(35))]));
        var again = await Resolver().ResolveAsync(Request(overrides: [("roster.max", ParameterValue.Of(35)), (_win, ParameterValue.Of(4))]));

        again.CanonicalJson.ShouldBe(first.CanonicalJson);
        again.Hash.ShouldBe(first.Hash);
    }

    [Fact]
    public async Task Stating_a_default_gives_the_hash_of_leaving_it_out()
    {
        var bare = await _resolver.ResolveAsync(Request());
        var stated = await _resolver.ResolveAsync(Request(
            parameters: [("cycles", ParameterValue.Of(1)), ("seeding", ParameterValue.Of("RANDOM"))],
            overrides: [(_win, ParameterValue.Of(3)), (_tiebreakers, League.Editable[_tiebreakers].Default)]));

        stated.Hash.ShouldBe(bare.Hash);
    }

    [Fact]
    public async Task Each_input_changes_the_hash()
    {
        var hashes = new[]
        {
            await _resolver.ResolveAsync(Request()),
            await _resolver.ResolveAsync(Request(overrides: (_win, ParameterValue.Of(4)))),
            await _resolver.ResolveAsync(Request(parameters: [("cycles", ParameterValue.Of(2))])),
            await _resolver.ResolveAsync(Request(format: "SINGLE_CONTEST")),
            await _resolver.ResolveAsync(Request(template: "FOOTBALL_CUP", format: "SINGLE_CONTEST")),
            await _resolver.ResolveAsync(Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "WINS", "DRAWING_OF_LOTS"])))),
        }.Select(snapshot => snapshot.Hash);

        hashes.Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public async Task The_canonical_json_is_rulesetsmd_layout_with_the_rules_beside_what_they_came_from()
    {
        var snapshot = await _resolver.ResolveAsync(Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "WINS", "DRAWING_OF_LOTS"]))));

        var root = JsonNode.Parse(snapshot.CanonicalJson)!.AsObject();
        root.Select(member => member.Key).ShouldBe(
            ["contest", "eligibility", "format", "lineup", "operations", "roster", "sport", "standings", "substitutions", "template"]);
        root["sport"]!.ToJsonString().ShouldBe($$"""{"hash":"{{Football.ContentHash}}","variant":"FOOTBALL_ASSOCIATION","version":1}""");
        root["format"]!.ToJsonString().ShouldBe("""{"code":"ROUND_ROBIN","parameters":{"cycles":1,"home_away_balance":true,"seeding":"RANDOM"},"version":1}""");
        root["template"]!.ToJsonString().ShouldBe("""{"code":"FOOTBALL_LEAGUE","version":1}""");
        root["standings"]!["tiebreakers"]!.ToJsonString().ShouldBe("""["POINTS","WINS","DRAWING_OF_LOTS"]""");
        root["standings"]!["h2h_reapply"]!.GetValue<bool>().ShouldBeTrue();
        root["roster"]!["lock_policy"]!.GetValue<string>().ShouldBe("FIRST_CONTEST");
        root["eligibility"]!.AsObject().ContainsKey("min_age").ShouldBeTrue();
        snapshot.Values.Standings.Tiebreakers.ShouldBe(["POINTS", "WINS", "DRAWING_OF_LOTS"]);
    }

    [Fact]
    public async Task A_template_resolves_against_the_sport_version_it_pins_after_a_newer_one_is_stored()
    {
        var newer = Football with { Version = 2, ContentHash = "sha256:" + new string('2', 64) };

        var snapshot = await Resolver(sports: [Football, newer]).ResolveAsync(Request());

        snapshot.Sport.ShouldBe(new RulesSnapshotSport("FOOTBALL_ASSOCIATION", 1, Football.ContentHash));
    }

    [Fact]
    public async Task The_latest_format_version_is_used_unless_one_is_given()
    {
        var second = GeneratorTestData.RoundRobin with { Version = 2 };
        var resolver = Resolver(formats: [GeneratorTestData.RoundRobin, second, GeneratorTestData.SingleContest]);

        (await resolver.ResolveAsync(Request())).Format.Version.ShouldBe(2);
        (await resolver.ResolveAsync(Request(formatVersion: 1))).Format.Version.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task An_invalid_combination_is_refused_with_its_code_on_its_field(string name)
    {
        var (resolver, request, expected) = _invalid[name];

        var resolution = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        resolution.IsSuccess.ShouldBeFalse(name);
        resolution.Problems.Select(problem => (problem.Field, problem.Error.Code)).ShouldBe(expected, name);
        resolution.Problems.ShouldAllBe(problem => !string.IsNullOrWhiteSpace(problem.Message));
    }

    [Fact]
    public async Task Every_resolver_error_has_a_case()
    {
        var codes = new List<string>();
        foreach (var (resolver, request, _) in _invalid.Values)
        {
            codes.AddRange((await resolver.ResolveAsync(request, TestContext.Current.CancellationToken)).Problems.Select(problem => problem.Error.Code));
        }

        codes.Distinct().Order(StringComparer.Ordinal).ShouldBe(
        [
            "FORMAT_PARAMETER_INVALID", "RULESET_INCOMPATIBLE_FORMAT", "RULESET_PARAMETER_OUT_OF_RANGE", "RULESET_PARAMETER_UNKNOWN",
            "RULESET_TEMPLATE_NOT_FOUND", "RULESET_TIE_RESOLUTION_UNSUPPORTED", "RULESET_UNKNOWN_TIEBREAKER",
        ]);
    }

    public static TheoryData<string> InvalidCases => [.. _invalid.Keys];

    private sealed record Case(RulesetResolver Resolver, RulesetResolutionRequest Request, (string Field, string Code)[] Expected);

    // A tie resolution a competition may pick, and a points value the schema refuses: neither football template allows
    // such a change, so these cases add the parameter to a copy (the template validator would refuse both files).
    private static readonly RulesetResolver _withTieResolution = Resolver(templates:
    [
        League,
        Cup.WithEditable("contest.tie_resolution", new ParameterDefinition(ParameterType.Choice, "Tie resolution", ParameterValue.Of("EXTRA_PERIODS_THEN_SHOOTOUT"))
        {
            Values = ["DRAW", "EXTRA_PERIODS_THEN_SHOOTOUT", "SHOOTOUT", "EXTRA_PERIODS_SUDDEN_DEATH"],
        }),
    ]);

    private static readonly RulesetResolver _withNegativeLoss = Resolver(templates:
    [
        League.WithEditable("standings.points.loss", new ParameterDefinition(ParameterType.Number, "Points for a loss", ParameterValue.Of(0), Min: -3, Max: 3)),
    ]);

    private static readonly Dictionary<string, Case> _invalid = new(StringComparer.Ordinal)
    {
        ["an unknown template"] = new(_resolver, Request(template: "NETBALL_LEAGUE"), [("template", "RULESET_TEMPLATE_NOT_FOUND")]),
        ["a path the template doesn't have"] = new(_resolver, Request(overrides: ("standings.points.bonus", ParameterValue.Of(1))),
            [("overrides.standings.points.bonus", "RULESET_PARAMETER_UNKNOWN")]),
        ["a value the template doesn't let a competition change"] = new(_resolver, Request(overrides: ("lineup.starters", ParameterValue.Of(7))),
            [("overrides.lineup.starters", "RULESET_PARAMETER_UNKNOWN")]),
        ["a number above its bound"] = new(_resolver, Request(overrides: (_win, ParameterValue.Of(6))),
            [("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a number below its bound"] = new(_resolver, Request(overrides: ("contest.period_seconds.FIRST_HALF", ParameterValue.Of(300))),
            [("overrides.contest.period_seconds.FIRST_HALF", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a value of the wrong type"] = new(_resolver, Request(overrides: (_win, ParameterValue.Of("FOUR"))),
            [("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a tiebreaker the template doesn't offer"] = new(_resolver, Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "DIFFERENCE"]))),
            [("overrides.standings.tiebreakers", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a tiebreaker not in the catalogue"] = new(_resolver, Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "FAIR_PLAY"]))),
            [("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")]),
        ["a tiebreaker for another sport"] = new(_resolver, Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "TRIES_SCORED"]))),
            [("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")]),
        ["a tiebreaker listed twice"] = new(_resolver, Request(overrides: (_tiebreakers, ParameterValue.Of(["POINTS", "WINS", "POINTS"]))),
            [("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")]),
        ["a smallest squad above the largest"] = new(_resolver, Request(overrides: ("roster.min", ParameterValue.Of(40))),
            [("overrides.roster.max", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a largest squad that can't hold the starters and the bench"] = new(_resolver, Request(overrides: ("roster.max", ParameterValue.Of(18))),
            [("overrides.roster.max", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a value the template schema refuses"] = new(_withNegativeLoss, Request(overrides: ("standings.points.loss", ParameterValue.Of(-1))),
            [("overrides.standings.points.loss", "RULESET_PARAMETER_OUT_OF_RANGE")]),
        ["a tie resolution the sport doesn't support"] = new(_withTieResolution,
            Request(template: "FOOTBALL_CUP", format: "KNOCKOUT", overrides: ("contest.tie_resolution", ParameterValue.Of("EXTRA_PERIODS_SUDDEN_DEATH"))),
            [("overrides.contest.tie_resolution", "RULESET_TIE_RESOLUTION_UNSUPPORTED")]),
        ["a tie resolution the template's extra periods don't fit"] = new(_withTieResolution,
            Request(template: "FOOTBALL_CUP", format: "KNOCKOUT", overrides: ("contest.tie_resolution", ParameterValue.Of("DRAW"))),
            [("overrides.contest.tie_resolution", "RULESET_TIE_RESOLUTION_UNSUPPORTED")]),
        ["a format the template doesn't list"] = new(_resolver, Request(format: "KNOCKOUT"), [("format.code", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format version that doesn't exist"] = new(_resolver, Request(formatVersion: 9), [("format.version", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format version that doesn't take the sport's contests"] = new(
            Resolver(formats: [GeneratorTestData.RoundRobin, GeneratorTestData.RoundRobin with { Version = 2, CompatibleShapes = [Modules.SportsConfiguration.Contracts.Sports.ContestShape.Field] }]),
            Request(formatVersion: 2), [("format.version", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format parameter out of range"] = new(_resolver, Request(parameters: [("cycles", ParameterValue.Of(9))]),
            [("format.parameters.cycles", "FORMAT_PARAMETER_INVALID")]),
        ["a format parameter the format doesn't have"] = new(_resolver, Request(parameters: [("legs", ParameterValue.Of(2))]),
            [("format.parameters.legs", "FORMAT_PARAMETER_INVALID")]),
        ["several problems at once, by field"] = new(_resolver,
            Request(format: "KNOCKOUT", overrides: [(_win, ParameterValue.Of(6)), ("lineup.starters", ParameterValue.Of(7))]),
            [("format.code", "RULESET_INCOMPATIBLE_FORMAT"), ("overrides.lineup.starters", "RULESET_PARAMETER_UNKNOWN"), ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")]),
    };
}
