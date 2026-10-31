using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

// sport-engine.md §5, "Outcome": the tie resolutions and steps the football scenarios can't reach (overtime until
// decided, a sequence used up, a reversed sudden-death score), with timelines folded as the scenarios fold theirs.
public sealed class AdditiveOutcomeTests
{
    private const string _cup = "BET ET1 ETB ET2 BP PEN";

    private static readonly ScoringEngines _engines = new([new AdditiveEngine()]);

    private static readonly AdditiveEngine _engine = new();

    // Periods by short label; "+" before one means it is still running, and "home:1H" a goal for the home side in it.
    public static TheoryData<string?, string?, string, OutcomeKind, string?> LevelScores() => new()
    {
        { null, null, "1H 2H", OutcomeKind.Draw, null },
        { TieResolutions.Draw, _cup, "1H 2H", OutcomeKind.Draw, null },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H +2H", OutcomeKind.RequiresExtraTime, "AWAITING_EXTRA_TIME" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H", OutcomeKind.RequiresExtraTime, "AWAITING_EXTRA_TIME" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET", OutcomeKind.RequiresExtraTime, "EXTRA_TIME_FIRST_HALF" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET ET1", OutcomeKind.RequiresExtraTime, "EXTRA_TIME_HALF_TIME" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET ET1 ETB ET2", OutcomeKind.RequiresShootout, "AWAITING_SHOOTOUT" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET ET1 ETB ET2 BP", OutcomeKind.RequiresShootout, "PENALTY_SHOOTOUT" },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET ET1 ETB ET2 BP +PEN", OutcomeKind.RequiresShootout, null },
        { TieResolutions.ExtraPeriodsThenShootout, _cup, "1H 2H BET ET1 ETB ET2 BP PEN", OutcomeKind.RequiresShootout, null },
        { TieResolutions.Shootout, "BP PEN", "1H 2H", OutcomeKind.RequiresShootout, "AWAITING_SHOOTOUT" },
        // Rules with extra time but no shoot-out: the last period is needed while it runs, then nothing is left.
        { TieResolutions.ExtraPeriodsThenShootout, "BET ET1 ETB ET2", "1H 2H BET ET1 ETB +ET2", OutcomeKind.RequiresExtraTime, null },
        { TieResolutions.ExtraPeriodsThenShootout, "BET ET1 ETB ET2", "1H 2H BET ET1 ETB ET2", OutcomeKind.Draw, null },
    };

    [Theory]
    [MemberData(nameof(LevelScores))]
    public void A_level_score_follows_the_tie_resolution_along_the_sequence(
        string? resolution, string? sequence, string periods, OutcomeKind kind, string? next)
    {
        var outcome = Outcome(Football, Rules(Football, resolution, sequence), periods);

        (outcome.Kind, outcome.NextPeriodCode, outcome.WinnerParticipantId).ShouldBe((kind, next, (Guid?)null));
    }

    [Theory]
    [InlineData("home:1H 2H", OutcomeMethod.Regulation)]
    [InlineData("1H 2H BET home:ET1", OutcomeMethod.ExtraTime)]
    // The away side's normal-time equaliser reversed after extra time started: extra time was played, so it was decided
    // there (owner, review #81 R5).
    [InlineData("home:1H 2H BET ET1", OutcomeMethod.ExtraTime)]
    [InlineData("1H 2H BET ET1 ETB ET2 BP home-shootout:PEN", OutcomeMethod.Shootout)]
    public void A_different_score_wins_by_where_the_contest_was_decided(string periods, OutcomeMethod method)
    {
        var outcome = Outcome(Football, Rules(Football, TieResolutions.ExtraPeriodsThenShootout, _cup), periods);

        (outcome.Kind, outcome.WinnerParticipantId, outcome.Method, outcome.RequiresEnd).ShouldBe((OutcomeKind.Winner, (Guid?)Home, (OutcomeMethod?)method, false));
    }

    [Fact]
    public void The_main_score_decides_before_the_shootout()
    {
        var outcome = Outcome(Football, Rules(Football, TieResolutions.ExtraPeriodsThenShootout, _cup), "1H 2H BET away:ET1 ETB ET2 BP home-shootout:PEN");

        (outcome.WinnerParticipantId, outcome.Method).ShouldBe(((Guid?)Away, (OutcomeMethod?)OutcomeMethod.ExtraTime));
    }

    [Theory]
    [InlineData("Q1 Q2 Q3 Q4", OutcomeKind.RequiresExtraTime, "AWAITING_OVERTIME")]
    [InlineData("Q1 Q2 Q3 Q4 BOT +OT", OutcomeKind.RequiresExtraTime, null)]
    [InlineData("Q1 Q2 Q3 Q4 BOT OT", OutcomeKind.RequiresExtraTime, "AWAITING_OVERTIME")]
    [InlineData("Q1 Q2 Q3 Q4 BOT OT BOT OT", OutcomeKind.RequiresExtraTime, "AWAITING_OVERTIME")]
    public void Repeated_extra_periods_go_on_until_decided(string periods, OutcomeKind kind, string? next)
    {
        var outcome = Outcome(Basketball, Rules(Basketball, TieResolutions.RepeatExtraPeriod, "BOT OT"), periods);

        (outcome.Kind, outcome.NextPeriodCode).ShouldBe((kind, next));
    }

    [Fact]
    public void A_win_in_a_repeated_extra_period_is_overtime()
    {
        var outcome = Outcome(Basketball, Rules(Basketball, TieResolutions.RepeatExtraPeriod, "BOT OT"), "Q1 Q2 Q3 Q4 BOT OT BOT away:OT");

        (outcome.WinnerParticipantId, outcome.Method).ShouldBe(((Guid?)Away, (OutcomeMethod?)OutcomeMethod.Overtime));
    }

    [Fact]
    public void A_reversed_sudden_death_score_no_longer_ends_the_contest()
    {
        var ctx = Context(RugbyUnion15) with { Rules = Rules(RugbyUnion15, TieResolutions.ExtraPeriodsSuddenDeath, "BET ET1 ETB ET2 SD") };
        var timeline = Timeline(RugbyUnion15, "1H 2H BET ET1 ETB ET2 +SD");
        var decided = Event(timeline.Count + 1, "PENALTY_GOAL", Home) with { PeriodCode = "SUDDEN_DEATH", PeriodOccurrence = 1 };
        timeline.Add(decided);

        var won = Propose(ctx, timeline);
        timeline.Add(Event(timeline.Count + 1, GenericEventCodes.EventReversed) with { ReversesEventId = decided.Id });
        var reversed = Propose(ctx, timeline);

        (won.Kind, won.Method, won.RequiresEnd).ShouldBe((OutcomeKind.Winner, (OutcomeMethod?)OutcomeMethod.SuddenDeath, true));
        (reversed.Kind, reversed.NextPeriodCode, reversed.RequiresEnd).ShouldBe((OutcomeKind.RequiresExtraTime, (string?)null, false));
    }

    private static OutcomeProposal Outcome(SportDefinition definition, ContestRules rules, string periods) =>
        Propose(Context(definition) with { Rules = rules }, Timeline(definition, periods));

    private static OutcomeProposal Propose(ContestContext ctx, List<TimelineEvent> timeline) =>
        _engine.ProposeOutcome(ctx, _engines.Fold(ctx, ContestStatus.Live, timeline).Score);

    private static ContestRules Rules(SportDefinition definition, string? resolution, string? sequence)
    {
        var values = new List<KeyValuePair<string, object>>();
        if (resolution is not null)
        {
            values.Add(KeyValuePair.Create(AdditiveEngine.TieResolutionRule, (object)resolution));
        }

        if (sequence is not null)
        {
            values.Add(KeyValuePair.Create(AdditiveEngine.ExtraPeriodSequenceRule, (object)sequence.Split(' ').Select(label => Code(definition, label)).ToList()));
        }

        return new ContestRules(values);
    }

    // Each period starts, takes its goals ("home:1H" scores 1 in the definition's first scoring type with points 1 of
    // the component, "home-shootout:PEN" in the shoot-out) and ends, unless marked "+" (still running).
    private static List<TimelineEvent> Timeline(SportDefinition definition, string periods)
    {
        var timeline = new List<TimelineEvent>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in periods.Split(' '))
        {
            var running = item.StartsWith('+');
            var (scorer, label) = item.TrimStart('+').Split(':') is [var who, var period] ? (who, period) : (null, item.TrimStart('+'));
            var code = Code(definition, label);
            var occurrence = occurrences[code] = occurrences.GetValueOrDefault(code) + 1;
            timeline.Add(Start(timeline.Count + 1, code, occurrence));
            if (scorer is not null)
            {
                var (side, component) = scorer.Split('-') is [var team, var named] ? (team, named) : (scorer, ScoringFamilies.MainComponent);
                var type = definition.EventTypes.First(candidate => candidate.Points == 1 && (candidate.Component ?? ScoringFamilies.MainComponent) == component);
                timeline.Add(Event(timeline.Count + 1, type.Code, side == "home" ? Home : Away) with { PeriodCode = code, PeriodOccurrence = occurrence });
            }

            if (!running)
            {
                timeline.Add(Event(timeline.Count + 1, GenericEventCodes.PeriodEnd) with { PeriodCode = code, PeriodOccurrence = occurrence });
            }
        }

        return timeline;
    }

    private static string Code(SportDefinition definition, string label) =>
        definition.Periods.First(period => period.ShortLabel == label).Code;
}
