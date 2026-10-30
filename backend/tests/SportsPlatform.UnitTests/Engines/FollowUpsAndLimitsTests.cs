using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

// The scenarios show follow-ups and limits as a match report; these are the edges: rules that aren't set or unknown,
// corrections in place, and which try a conversion used. BASIC coverage, so nobody needs naming.
public sealed class FollowUpsAndLimitsTests
{
    private static readonly ScoringEngines _engines = new([new AdditiveEngine()]);

    private static readonly string _missing = SportsConfigurationErrors.TimelineFollowUpMissing.Code;

    private static readonly string _reached = SportsConfigurationErrors.TimelineLimitReached.Code;

    [Fact]
    public void A_conversion_follows_its_own_sides_try_in_the_same_period()
    {
        var ctx = Context(RugbyUnion15) with { Coverage = CoverageLevel.Basic };

        Codes(ctx, [Start(1, "FIRST_HALF"), Played(2, "TRY", Away)], Scored("CONVERSION_MADE", Home)).ShouldBe([_missing]);
        Codes(ctx, [Start(1, "FIRST_HALF"), Played(2, "TRY", Home), End(3), Start(4, "HALF_TIME"), End(5), Start(6, "SECOND_HALF")], Scored("CONVERSION_MADE", Home))
            .ShouldBe([_missing]);
        Codes(ctx, [Start(1, "FIRST_HALF"), Played(2, "TRY", Home)], Scored("CONVERSION_MADE", Home)).ShouldBeEmpty();
    }

    [Fact]
    public void A_try_is_used_once_and_a_reversed_conversion_gives_it_back()
    {
        var ctx = Context(RugbyUnion15) with { Coverage = CoverageLevel.Basic };
        var timeline = new[] { Start(1, "FIRST_HALF"), Played(2, "TRY", Home), Played(3, "CONVERSION_MISSED", Home) };

        Codes(ctx, timeline, Scored("CONVERSION_MADE", Home)).ShouldBe([_missing]);
        Codes(ctx, [.. timeline, Event(4, GenericEventCodes.EventReversed) with { ReversesEventId = Id(3) }], Scored("CONVERSION_MADE", Home)).ShouldBeEmpty();
        // A correction is checked as at the event it replaces, where the try was still unused.
        Codes(ctx, timeline, Scored("CONVERSION_MADE", Home) with { SupersedesEventId = Id(3) }).ShouldBeEmpty();
    }

    [Fact]
    public void A_limit_the_rules_dont_set_is_no_limit()
    {
        var ctx = Context(Football) with { Coverage = CoverageLevel.Basic };

        Codes(ctx, [Start(1, "FIRST_HALF"), .. Substitutions(2, 20)], Scored("SUBSTITUTION", Home)).ShouldBeEmpty();
    }

    [Fact]
    public void A_correction_counts_in_the_place_of_the_substitution_it_replaces()
    {
        var ctx = Context(Football) with { Coverage = CoverageLevel.Basic, Rules = Rules(("substitutions.max", 2L), ("substitutions.windows", 1L)) };
        TimelineEvent[] timeline = [Start(1, "FIRST_HALF"), .. Substitutions(2, 2)];

        Codes(ctx, timeline, Scored("SUBSTITUTION", Home)).ShouldBe([_reached]);
        Codes(ctx, timeline, Scored("SUBSTITUTION", Home) with { SupersedesEventId = Id(2) }).ShouldBeEmpty();
        Codes(ctx, [.. timeline, Event(4, GenericEventCodes.EventReversed) with { ReversesEventId = Id(3) }], Scored("SUBSTITUTION", Home)).ShouldBeEmpty();
    }

    [Fact]
    public void A_limit_of_a_kind_the_engines_dont_know_is_a_bug()
    {
        var type = Football.EventTypes.Single(candidate => candidate.Code == "SUBSTITUTION");
        var definition = Football with
        {
            EventTypes = [.. Football.EventTypes.Select(candidate => candidate == type ? type with { Limits = [new EventLimitDefinition("substitutions.per_hour")] } : candidate)],
        };
        var ctx = Context(definition) with { Coverage = CoverageLevel.Basic, Rules = Rules(("substitutions.per_hour", 1L)) };

        Should.Throw<InvalidOperationException>(() => Codes(ctx, [Start(1, "FIRST_HALF")], Scored("SUBSTITUTION", Home)))
            .Message.ShouldContain("per_hour");
    }

    private static string[] Codes(ContestContext ctx, TimelineEvent[] timeline, TimelineEventDraft draft) =>
        [.. new AdditiveEngine().Validate(ctx, _engines.Fold(ctx, ContestStatus.Live, timeline), draft).Errors.Select(error => error.Code)];

    private static TimelineEventDraft Scored(string type, Guid participant) => new(type, TimelineSource.Live) { ParticipantId = participant };

    private static TimelineEvent Played(long sequence, string type, Guid participant, string period = "FIRST_HALF") =>
        Event(sequence, type, participant) with { PeriodCode = period, PeriodOccurrence = 1 };

    private static TimelineEvent End(long sequence) => Event(sequence, GenericEventCodes.PeriodEnd);

    // The home side's substitutions in a row, from the given sequence.
    private static TimelineEvent[] Substitutions(long from, int count) =>
        [.. Enumerable.Range(0, count).Select(index => Played(from + index, "SUBSTITUTION", Home))];

    private static ContestRules Rules(params (string Path, object Value)[] rules) =>
        new(rules.Select(rule => KeyValuePair.Create(rule.Path, rule.Value)));
}
