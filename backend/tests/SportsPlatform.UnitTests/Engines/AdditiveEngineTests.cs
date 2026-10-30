using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

// Scoring itself is covered by the scenarios; these are the cases a scenario can't record.
public sealed class AdditiveEngineTests
{
    private static readonly AdditiveEngine _engine = new();

    [Theory]
    [InlineData("GOAL")]
    [InlineData("OWN_GOAL")]
    public void A_scoring_event_from_outside_the_contest_is_a_bug(string type)
    {
        var ctx = Context(Football);

        Should.Throw<InvalidOperationException>(() => _engine.Apply(ctx, _engine.Initial(ctx), Event(1, type, Person(99))));
    }

    [Fact]
    public void Events_that_score_nothing_leave_the_state_as_it_was()
    {
        var ctx = Context(Football);
        var initial = _engine.Initial(ctx);

        _engine.Apply(ctx, initial, Event(1, "PENALTY_MISSED", Home)).ShouldBeSameAs(initial);
    }

    [Fact]
    public void The_rules_may_change_what_a_scoring_event_is_worth_but_not_make_another_score()
    {
        var ctx = Context(Football) with
        {
            Rules = new ContestRules([KeyValuePair.Create("scoring.points.GOAL", (object)2L), KeyValuePair.Create("scoring.points.PENALTY_MISSED", (object)1L)]),
        };
        var goal = Event(1, "GOAL", Home) with { PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 };
        var state = _engine.Apply(ctx, _engine.Initial(ctx), goal);

        _engine.Summarize(ctx, _engine.Apply(ctx, state, Event(2, "PENALTY_MISSED", Home))).Participants[0].Primary.ShouldBe("2");
    }

    [Fact]
    public void A_scoring_event_worth_nothing_by_the_rules_scores_nothing()
    {
        var ctx = Context(Football) with { Rules = new ContestRules([KeyValuePair.Create("scoring.points.GOAL", (object)0L)]) };
        var initial = _engine.Initial(ctx);

        _engine.Apply(ctx, initial, Event(1, "GOAL", Home)).ShouldBeSameAs(initial);
    }
}
