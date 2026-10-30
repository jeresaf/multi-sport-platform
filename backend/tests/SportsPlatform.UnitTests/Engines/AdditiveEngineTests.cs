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
}
