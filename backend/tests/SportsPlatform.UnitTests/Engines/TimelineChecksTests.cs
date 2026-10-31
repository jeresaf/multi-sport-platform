using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

// The scenarios read like match reports; these are the cases a scenario can't record (a participant from outside the
// contest, statuses other than live and ended, opponents' roles, references to generic events).
public sealed class TimelineChecksTests
{
    private static readonly ScoringEngines _engines = new([new AdditiveEngine()]);

    private static readonly ContestLineup _home = new(Home, [Person(1), Person(2)], [Person(3)]) { Staff = [Person(4)] };

    private static readonly ContestLineup _away = new(Away, [Person(11), Person(12)], [Person(13)]);

    private static ContestContext Ctx => Context(Football, _home, _away);

    [Theory]
    [InlineData(ContestStatus.NotStarted)]
    [InlineData(ContestStatus.Interrupted)]
    [InlineData(ContestStatus.Suspended)]
    [InlineData(ContestStatus.Abandoned)]
    [InlineData(ContestStatus.Ended)]
    [InlineData(ContestStatus.Closed)]
    public void Only_a_live_contest_takes_events_from_the_scorer(ContestStatus status)
    {
        Codes(Validate(status, [], Draft("NOTE"))).ShouldBe([SportsConfigurationErrors.TimelineContestNotLive.Code]);
        Codes(Validate(status, [], Draft("NOTE", TimelineSource.OfflineSync))).ShouldBe([SportsConfigurationErrors.TimelineContestNotLive.Code]);
    }

    [Theory]
    [InlineData(TimelineSource.Backfill)]
    [InlineData(TimelineSource.Admin)]
    public void Backfill_and_administrators_record_in_any_status(TimelineSource source)
    {
        Validate(ContestStatus.Closed, [], Draft("NOTE", source)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_ended_contest_takes_only_corrections_and_reversals_while_the_correction_window_is_open()
    {
        var timeline = new[]
        {
            Start(1, "FIRST_HALF"),
            Event(2, "GOAL", Home) with { Actors = Scorer(Person(1)), PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 },
            Event(3, GenericEventCodes.PeriodEnd) with { PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 },
        };
        var live = _engines.Fold(Ctx, ContestStatus.Ended, timeline) with { CorrectionWindowOpen = true };
        var engine = new AdditiveEngine();

        engine.Validate(Ctx, live, Draft("GOAL") with { ParticipantId = Home, Actors = Scorer(Person(2)), SupersedesEventId = Id(2) }).IsValid.ShouldBeTrue();
        engine.Validate(Ctx, live, Draft("EVENT_REVERSED") with { ReversesEventId = Id(2) }).IsValid.ShouldBeTrue();
        Codes(engine.Validate(Ctx, live, Draft("PERIOD_START") with { PeriodCode = "SECOND_HALF" })).ShouldBe([SportsConfigurationErrors.TimelineContestNotLive.Code]);
        Codes(engine.Validate(Ctx, live, Draft("NOTE"))).ShouldBe([SportsConfigurationErrors.TimelineContestNotLive.Code]);
    }

    [Fact]
    public void A_participant_from_outside_the_contest_is_refused_even_on_events_that_need_none()
    {
        Codes(Validate(ContestStatus.Live, [Start(1, "FIRST_HALF")], Draft("GOAL") with { ParticipantId = Person(99), Actors = Scorer(Person(1)) }))
            .ShouldBe([SportsConfigurationErrors.TimelineParticipantInvalid.Code]);
        Codes(Validate(ContestStatus.Live, [], Draft("NOTE") with { ParticipantId = Person(99) }))
            .ShouldBe([SportsConfigurationErrors.TimelineParticipantInvalid.Code]);
    }

    [Fact]
    public void Events_of_the_contest_as_a_whole_need_no_participant()
    {
        Validate(ContestStatus.Live, [Start(1, "FIRST_HALF")], Draft("VAR_REVIEW")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_role_on_the_opponents_side_is_checked_against_the_opponents_field()
    {
        var timeline = new[] { Start(1, "FIRST_HALF") };
        var foul = Draft("FOUL") with { ParticipantId = Home };
        var ctx = Ctx with { Coverage = CoverageLevel.Detailed };

        Validate(ctx, timeline, foul with { Actors = Roles(("OFFENDER", Person(1)), ("FOULED", Person(11))) }).IsValid.ShouldBeTrue();
        Codes(Validate(ctx, timeline, foul with { Actors = Roles(("OFFENDER", Person(1)), ("FOULED", Person(2))) }))
            .ShouldBe([SportsConfigurationErrors.TimelineActorNotOnField.Code]);
    }

    [Fact]
    public void Staff_can_be_shown_a_card_but_not_score()
    {
        var timeline = new[] { Start(1, "FIRST_HALF") };

        Validate(ContestStatus.Live, timeline, Draft("YELLOW_CARD") with { ParticipantId = Home, Actors = Roles(("OFFENDER", Person(4))) }).IsValid.ShouldBeTrue();
        Codes(Validate(ContestStatus.Live, timeline, Draft("GOAL") with { ParticipantId = Home, Actors = Scorer(Person(4)) }))
            .ShouldBe([SportsConfigurationErrors.TimelineActorNotOnField.Code]);
    }

    [Fact]
    public void Generic_events_name_nobody()
    {
        Codes(Validate(ContestStatus.Live, [], Draft("NOTE") with { Actors = Roles(("PLAYER", Person(1))) }))
            .ShouldBe([SportsConfigurationErrors.TimelineActorInvalid.Code]);
    }

    [Fact]
    public void A_missing_role_and_a_wrong_one_are_both_reported()
    {
        Codes(Validate(ContestStatus.Live, [Start(1, "FIRST_HALF")], Draft("GOAL") with { ParticipantId = Home, Actors = Roles(("TAKER", Person(1))) }))
            .ShouldBe([SportsConfigurationErrors.TimelineActorRequired.Code, SportsConfigurationErrors.TimelineActorInvalid.Code]);
    }

    [Fact]
    public void Generic_events_are_reversed_never_corrected()
    {
        var timeline = new[] { Start(1, "FIRST_HALF"), Event(2, "GOAL", Home) with { Actors = Scorer(Person(1)), PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 } };

        Codes(Validate(ContestStatus.Live, timeline, Draft("GOAL") with { ParticipantId = Home, Actors = Scorer(Person(1)), SupersedesEventId = Id(1) }))
            .ShouldBe([SportsConfigurationErrors.TimelineReferenceInvalid.Code]);
        Codes(Validate(ContestStatus.Live, timeline, Draft("NOTE") with { SupersedesEventId = Id(2) }))
            .ShouldBe([SportsConfigurationErrors.TimelineReferenceInvalid.Code]);
        Validate(ContestStatus.Live, timeline, Draft("EVENT_REVERSED") with { ReversesEventId = Id(1) }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_event_either_corrects_or_reverses_and_names_an_event_of_the_contest()
    {
        var timeline = new[] { Start(1, "FIRST_HALF"), Event(2, "GOAL", Home) with { Actors = Scorer(Person(1)), PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 } };

        Codes(Validate(ContestStatus.Live, timeline, Draft("EVENT_REVERSED") with { ReversesEventId = Id(2), SupersedesEventId = Id(2) }))
            .ShouldBe([SportsConfigurationErrors.TimelineReferenceInvalid.Code]);
        Codes(Validate(ContestStatus.Live, timeline, Draft("EVENT_REVERSED") with { ReversesEventId = Id(99) }))
            .ShouldBe([SportsConfigurationErrors.TimelineReferenceInvalid.Code]);
        Codes(Validate(ContestStatus.Live, timeline, Draft("GOAL") with { ParticipantId = Home, Actors = Scorer(Person(2)), SupersedesEventId = Id(99) }))
            .ShouldBe([SportsConfigurationErrors.TimelineReferenceInvalid.Code]);
    }

    [Fact]
    public void The_score_that_decides_a_sudden_death_period_can_still_be_corrected_or_reversed()
    {
        var ctx = Context(RugbyUnion15, _home, _away);
        var timeline = new[]
        {
            Start(1, "SUDDEN_DEATH"),
            Event(2, "TRY", Home) with { Actors = Scorer(Person(1)), PeriodCode = "SUDDEN_DEATH", PeriodOccurrence = 1 },
        };

        Validate(ctx, timeline, Draft("TRY") with { ParticipantId = Home, Actors = Scorer(Person(2)), SupersedesEventId = Id(2) }).IsValid.ShouldBeTrue();
        Validate(ctx, timeline, Draft("EVENT_REVERSED") with { ReversesEventId = Id(2) }).IsValid.ShouldBeTrue();
        Codes(Validate(ctx, timeline, Draft("PENALTY_GOAL") with { ParticipantId = Away, Actors = Roles(("KICKER", Person(11))) }))
            .ShouldBe([SportsConfigurationErrors.TimelineEventNotAllowedInPhase.Code]);
    }

    [Fact]
    public void Play_goes_on_in_sudden_death_once_a_correction_of_an_earlier_period_levels_the_totals()
    {
        // 3–3 after normal time, a home penalty in sudden death, then the home first-half penalty is reversed: 3–3 again.
        var ctx = Context(RugbyUnion15, _home, _away);
        var decided = new[]
        {
            Start(1, "FIRST_HALF"),
            Event(2, "PENALTY_GOAL", Home) with { Actors = Roles(("KICKER", Person(1))), PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 },
            Event(3, GenericEventCodes.PeriodEnd) with { PeriodCode = "FIRST_HALF", PeriodOccurrence = 1 },
            Start(4, "SECOND_HALF"),
            Event(5, "PENALTY_GOAL", Away) with { Actors = Roles(("KICKER", Person(11))), PeriodCode = "SECOND_HALF", PeriodOccurrence = 1 },
            Event(6, GenericEventCodes.PeriodEnd) with { PeriodCode = "SECOND_HALF", PeriodOccurrence = 1 },
            Start(7, "SUDDEN_DEATH"),
            Event(8, "PENALTY_GOAL", Home) with { Actors = Roles(("KICKER", Person(1))), PeriodCode = "SUDDEN_DEATH", PeriodOccurrence = 1 },
        };
        var levelled = decided.Append(Event(9, GenericEventCodes.EventReversed) with { ReversesEventId = Id(2) }).ToArray();
        var awayTry = Draft("TRY") with { ParticipantId = Away, Actors = Scorer(Person(11)) };

        Codes(Validate(ctx, decided, awayTry)).ShouldBe([SportsConfigurationErrors.TimelineEventNotAllowedInPhase.Code]);
        Validate(ctx, levelled, awayTry).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_sudden_death_score_the_rules_make_worth_nothing_decides_nothing()
    {
        var ctx = Context(RugbyUnion15, _home, _away) with { Rules = new ContestRules([KeyValuePair.Create("scoring.points.PENALTY_GOAL", (object)0L)]) };
        var timeline = new[]
        {
            Start(1, "SUDDEN_DEATH"),
            Event(2, "PENALTY_GOAL", Home) with { Actors = Roles(("KICKER", Person(1))), PeriodCode = "SUDDEN_DEATH", PeriodOccurrence = 1 },
        };

        Validate(ctx, timeline, Draft("TRY") with { ParticipantId = Away, Actors = Scorer(Person(11)) }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Each_occurrence_of_a_repeated_sudden_death_period_is_decided_by_its_own_first_score()
    {
        var ctx = Context(RugbyUnion7, _home, _away);
        var scoreless = new[] { Start(1, "EXTRA_TIME"), Event(2, GenericEventCodes.PeriodEnd) with { PeriodCode = "EXTRA_TIME", PeriodOccurrence = 1 }, Start(3, "EXTRA_TIME", 2) };
        var decided = scoreless.Append(Event(4, "TRY", Home) with { Actors = Scorer(Person(1)), PeriodCode = "EXTRA_TIME", PeriodOccurrence = 2 }).ToArray();

        Validate(ctx, scoreless, Draft("TRY") with { ParticipantId = Home, Actors = Scorer(Person(1)) }).IsValid.ShouldBeTrue();
        Codes(Validate(ctx, decided, Draft("TRY") with { ParticipantId = Away, Actors = Scorer(Person(11)) }))
            .ShouldBe([SportsConfigurationErrors.TimelineEventNotAllowedInPhase.Code]);
    }

    private static ValidationResult Validate(ContestStatus status, TimelineEvent[] timeline, TimelineEventDraft draft) =>
        Validate(Ctx, timeline, draft, status);

    private static ValidationResult Validate(ContestContext ctx, TimelineEvent[] timeline, TimelineEventDraft draft, ContestStatus status = ContestStatus.Live) =>
        new AdditiveEngine().Validate(ctx, _engines.Fold(ctx, status, timeline), draft);

    private static TimelineEventDraft Draft(string type, TimelineSource source = TimelineSource.Live) => new(type, source);

    private static Dictionary<string, Guid> Scorer(Guid person) => Roles(("SCORER", person));

    private static Dictionary<string, Guid> Roles(params (string Role, Guid Person)[] actors) =>
        actors.ToDictionary(actor => actor.Role, actor => actor.Person);

    private static string[] Codes(ValidationResult result) => [.. result.Errors.Select(error => error.Code)];
}
