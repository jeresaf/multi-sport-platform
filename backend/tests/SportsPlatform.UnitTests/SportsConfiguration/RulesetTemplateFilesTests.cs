using System.Text.Json;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Hashing;

namespace SportsPlatform.UnitTests.SportsConfiguration;

public sealed class RulesetTemplateFilesTests
{
    private static readonly RulesetTemplateFiles _files = new();

    // Changing any content of a published template without bumping its version fails here before any database (S05 D4):
    // a new version gets a new hash and a new pin. Comments and layout are not content.
    [Theory]
    [InlineData("football-league.yaml", "FOOTBALL_LEAGUE", "sha256:94209d9d45af4e816c59805ca28023081528411f7828434e2cbafb66c5b7db5a")]
    [InlineData("football-cup.yaml", "FOOTBALL_CUP", "sha256:379e6050cb2ed959815f33cef747ba2ecce1bb237383507bd108364dfcf4da4c")]
    public void Each_template_at_version_1_has_its_published_hash(string file, string code, string hash)
    {
        var content = YamlJson.Parse(_files.Read(file));

        var template = RulesetTemplateContent.Read(content!);
        (template.Code, template.Version).ShouldBe((code, 1));
        CanonicalJson.Hash(content).ShouldBe(hash);
    }

    [Fact]
    public void The_league_reads_into_its_typed_model_with_defaults_from_its_values()
    {
        var league = Read("football-league.yaml");

        (league.Name, league.Sport).ShouldBe(("Football league", new RulesetSportReference("FOOTBALL_ASSOCIATION", 1)));
        league.Formats.ShouldBe(["ROUND_ROBIN", "SINGLE_CONTEST"]);
        league.Editable.Keys.Order(StringComparer.Ordinal).ShouldBe(
        [
            "contest.period_seconds.FIRST_HALF", "contest.period_seconds.SECOND_HALF", "lineup.bench_max", "roster.max", "roster.min",
            "standings.points.win", "standings.tiebreakers", "substitutions.max",
        ]);
        var win = league.Editable["standings.points.win"];
        (win.Type, win.Label, win.Min, win.Max, win.Default).ShouldBe((ParameterType.Number, "Points for a win", 1, 5, ParameterValue.Of(3)));
        var tiebreakers = league.Editable["standings.tiebreakers"];
        tiebreakers.Type.ShouldBe(ParameterType.List);
        tiebreakers.Values.ShouldBe(["POINTS", "GOAL_DIFFERENCE", "GOALS_FOR", "WINS", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS"]);
        tiebreakers.Default.ShouldBe(ParameterValue.Of(["POINTS", "GOAL_DIFFERENCE", "GOALS_FOR", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS"]));
        league.Editable["contest.period_seconds.FIRST_HALF"].Default.ShouldBe(ParameterValue.Of(2700));

        var values = league.Values;
        values.Contest.ShouldBe(values.Contest with { TieResolution = TieResolutions.Draw, CoverageDefault = CoverageLevel.Standard });
        values.Contest.PeriodSeconds.ShouldBe(new Dictionary<string, int> { ["FIRST_HALF"] = 2700, ["HALF_TIME"] = 900, ["SECOND_HALF"] = 2700 });
        values.Contest.ExtraPeriodSequence.ShouldBeEmpty();
        (values.Lineup.Starters, values.Lineup.BenchMax, values.Lineup.RequiresApproval).ShouldBe((11, 9, false));
        values.Lineup.Positions["GK"].ShouldBe(new CountRange(1, 1));
        values.Substitutions.ShouldBe(new RulesetSubstitutions(5, 3, 1));
        values.Roster.ShouldBe(new RulesetRoster(18, 30, true, RosterLockPolicy.FirstContest));
        values.Standings.Points.ShouldBe(new StandingsPoints(3, 1, 0, 0, 3, 0));
        values.Standings.ForfeitScore.ShouldBe(new ForfeitScore(3, 0));
        values.Standings.HeadToHeadReapply.ShouldBeTrue();
        values.Operations.Readiness.ShouldBe([ReadinessCheck.LineupsApproved, ReadinessCheck.OfficialsMinimum, ReadinessCheck.VenueSet]);
        values.Operations.OfficialsMinimum.ShouldBe(new Dictionary<string, int> { ["REFEREE"] = 1 });
        (values.Operations.ScorerCorrectionWindowMinutes, values.Operations.StartingSoonMinutes).ShouldBe((60, 30));
        values.Eligibility.ShouldBe(new RulesetEligibility(true, null, null, null, MinorConsentRequirement.TenantDefault));
    }

    [Fact]
    public void The_cup_plays_extra_time_then_penalties_in_a_knockout_or_a_single_contest()
    {
        var cup = Read("football-cup.yaml");

        cup.Formats.ShouldBe(["KNOCKOUT", "SINGLE_CONTEST"]);
        cup.Values.Contest.TieResolution.ShouldBe(TieResolutions.ExtraPeriodsThenShootout);
        cup.Values.Contest.ExtraPeriodSequence.ShouldBe(
            ["AWAITING_EXTRA_TIME", "EXTRA_TIME_FIRST_HALF", "EXTRA_TIME_HALF_TIME", "EXTRA_TIME_SECOND_HALF", "AWAITING_SHOOTOUT", "PENALTY_SHOOTOUT"]);
        cup.Values.Contest.PeriodSeconds["EXTRA_TIME_FIRST_HALF"].ShouldBe(900);
        cup.Values.Substitutions.ExtraInExtraTime.ShouldBe(1);
    }

    [Fact]
    public void An_age_reference_date_reads_as_a_date()
    {
        var content = YamlJson.Parse(_files.Read("football-league.yaml"))!.AsObject();
        content["values"]!["eligibility"]!["age_reference_date"] = "2027-01-01";
        content["values"]!["eligibility"]!["max_age"] = 17;

        var eligibility = RulesetTemplateContent.Read(content).Values.Eligibility;

        (eligibility.MaxAge, eligibility.AgeReferenceDate).ShouldBe((17, new DateOnly(2027, 1, 1)));
    }

    // The model refuses a key it doesn't map, so a schema change without a model change fails loudly.
    [Fact]
    public void The_model_refuses_an_unmapped_key()
    {
        var content = YamlJson.Parse(_files.Read("football-league.yaml"))!.AsObject();
        content["values"]!["standings"]!["bonus"] = 1;

        Should.Throw<JsonException>(() => RulesetTemplateContent.Read(content));
    }

    [Fact]
    public void An_editable_parameter_without_a_value_can_not_be_read()
    {
        var content = YamlJson.Parse(_files.Read("football-league.yaml"))!.AsObject();
        content["values"]!["roster"]!.AsObject().Remove("min");

        Should.Throw<JsonException>(() => RulesetTemplateContent.Read(content)).Message.ShouldBe("The editable parameter roster.min has no value.");
    }

    [Fact]
    public void A_list_value_is_a_JSON_array_of_codes_and_equal_by_its_items_in_order()
    {
        var value = JsonSerializer.Deserialize<ParameterValue>("""["POINTS","WINS"]""")!;

        (value.Type, value.ToString()).ShouldBe((ParameterType.List, "[POINTS, WINS]"));
        JsonSerializer.Serialize(value).ShouldBe("""["POINTS","WINS"]""");
        value.ShouldBe(ParameterValue.Of(["POINTS", "WINS"]));
        value.GetHashCode().ShouldBe(ParameterValue.Of(["POINTS", "WINS"]).GetHashCode());
        value.ShouldNotBe(ParameterValue.Of(["WINS", "POINTS"]));
        JsonSerializer.Deserialize<ParameterValue>("[]")!.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""[1]""")]
    [InlineData("""["POINTS", ""]""")]
    [InlineData("""[["POINTS"]]""")]
    public void A_list_holds_only_codes(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ParameterValue>(json));
    }

    private static RulesetTemplate Read(string file) => RulesetTemplateContent.Read(YamlJson.Parse(_files.Read(file))!);
}
