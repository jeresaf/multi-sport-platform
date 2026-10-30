using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

public sealed class PhaseTrackerTests
{
    [Fact]
    public void Before_the_first_start_there_is_no_period()
    {
        PhaseTracker.Track(Football, []).ShouldBe(ContestPhase.NotStarted);
    }

    [Fact]
    public void A_start_runs_the_period_it_names_with_its_kind()
    {
        var phase = PhaseTracker.Track(Football, [Start(1, "FIRST_HALF")]);

        (phase.PeriodCode, phase.Occurrence, phase.Running, phase.Kind).ShouldBe(("FIRST_HALF", 1, true, (PhaseKind?)PhaseKind.Play));
    }

    [Fact]
    public void After_an_end_and_before_the_next_start_the_phase_is_a_break()
    {
        var phase = PhaseTracker.Track(Football, [Start(1, "FIRST_HALF"), Event(2, GenericEventCodes.PeriodEnd)]);

        (phase.PeriodCode, phase.Running, phase.Kind).ShouldBe(("FIRST_HALF", false, (PhaseKind?)PhaseKind.Break));
    }

    [Fact]
    public void Break_periods_are_started_like_any_other()
    {
        var phase = PhaseTracker.Track(Football, [Start(1, "FIRST_HALF"), Event(2, GenericEventCodes.PeriodEnd), Start(3, "HALF_TIME")]);

        (phase.PeriodCode, phase.Running, phase.Kind).ShouldBe(("HALF_TIME", true, (PhaseKind?)PhaseKind.Break));
    }

    [Fact]
    public void A_period_started_again_is_its_next_occurrence_and_every_start_is_kept()
    {
        var phase = PhaseTracker.Track(Football, [Start(1, "FIRST_HALF"), Event(2, GenericEventCodes.PeriodEnd), Start(3, "FIRST_HALF", occurrence: 2)]);

        phase.Occurrence.ShouldBe(2);
        phase.Played.ShouldBe([new PlayedPeriod("FIRST_HALF", 1), new PlayedPeriod("FIRST_HALF", 2)]);
    }

    // Review #78 R2: the occurrence is the one recorded on the start, as the score's periods read it, so reversing an
    // earlier start doesn't renumber the later one.
    [Fact]
    public void A_start_keeps_its_recorded_occurrence_when_an_earlier_start_is_reversed()
    {
        var ctx = Context(Football);
        var live = new ScoringEngines([new AdditiveEngine()]).Fold(ctx, ContestStatus.Live,
        [
            Start(1, "FIRST_HALF"),
            Event(2, GenericEventCodes.PeriodEnd) with { PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 },
            Start(3, "FIRST_HALF", occurrence: 2),
            Event(4, GenericEventCodes.EventReversed) with { ReversesEventId = Id(1) },
        ]);

        (live.Phase.PeriodCode, live.Phase.Occurrence).ShouldBe(("FIRST_HALF", 2));
        live.Phase.Played.ShouldBe(((AdditiveScoreState)live.Score).Periods);
    }

    [Fact]
    public void An_unknown_period_is_a_bug()
    {
        Should.Throw<InvalidOperationException>(() => PhaseTracker.Track(Football, [Start(1, "FOURTH_QUARTER")]));
    }
}
