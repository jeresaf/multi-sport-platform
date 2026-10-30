using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

// sport-engine.md §3: REVERSED if a later event reverses it, else SUPERSEDED if a later event supersedes it, else ACTIVE.
public sealed class TimelineFoldTests
{
    [Fact]
    public void Events_without_references_are_active_in_sequence_order()
    {
        var entries = TimelineFold.Resolve([Event(2, "GOAL", Home), Event(1, "GOAL", Away)]);

        entries.Select(entry => (entry.Event.Sequence, entry.Status)).ShouldBe([(1L, EffectiveStatus.Active), (2L, EffectiveStatus.Active)]);
    }

    [Fact]
    public void A_correction_supersedes_the_event_it_replaces()
    {
        var entries = TimelineFold.Resolve([Event(1, "GOAL", Home), Event(2, "GOAL", Home) with { SupersedesEventId = Id(1) }]);

        Statuses(entries).ShouldBe([EffectiveStatus.Superseded, EffectiveStatus.Active]);
    }

    [Fact]
    public void A_reversal_reverses_the_event_and_stays_active_itself()
    {
        var entries = TimelineFold.Resolve([Event(1, "GOAL", Home), Event(2, "VAR_DECISION") with { ReversesEventId = Id(1) }]);

        Statuses(entries).ShouldBe([EffectiveStatus.Reversed, EffectiveStatus.Active]);
    }

    [Fact]
    public void A_chain_of_corrections_ends_at_its_last_one()
    {
        var entries = TimelineFold.Resolve(
        [
            Event(1, "GOAL", Home),
            Event(2, "GOAL", Home) with { SupersedesEventId = Id(1) },
            Event(3, "GOAL", Home) with { SupersedesEventId = Id(2) },
        ]);

        Statuses(entries).ShouldBe([EffectiveStatus.Superseded, EffectiveStatus.Superseded, EffectiveStatus.Active]);
    }

    [Fact]
    public void Reversing_a_correction_restores_the_event_it_had_replaced()
    {
        var entries = TimelineFold.Resolve(
        [
            Event(1, "GOAL", Home),
            Event(2, "GOAL", Home) with { SupersedesEventId = Id(1) },
            Event(3, GenericEventCodes.EventReversed) with { ReversesEventId = Id(2) },
        ]);

        Statuses(entries).ShouldBe([EffectiveStatus.Active, EffectiveStatus.Reversed, EffectiveStatus.Active]);
    }

    [Fact]
    public void Reversing_a_reversal_restores_the_event_it_had_reversed()
    {
        var entries = TimelineFold.Resolve(
        [
            Event(1, "GOAL", Home),
            Event(2, "VAR_DECISION") with { ReversesEventId = Id(1) },
            Event(3, GenericEventCodes.EventReversed) with { ReversesEventId = Id(2) },
        ]);

        Statuses(entries).ShouldBe([EffectiveStatus.Active, EffectiveStatus.Reversed, EffectiveStatus.Active]);
    }

    [Fact]
    public void A_superseded_reversal_no_longer_reverses()
    {
        var entries = TimelineFold.Resolve(
        [
            Event(1, "GOAL", Home),
            Event(2, "VAR_DECISION") with { ReversesEventId = Id(1) },
            Event(3, "VAR_DECISION") with { SupersedesEventId = Id(2) },
        ]);

        Statuses(entries).ShouldBe([EffectiveStatus.Active, EffectiveStatus.Superseded, EffectiveStatus.Active]);
    }

    [Fact]
    public void Only_a_later_event_counts()
    {
        var entries = TimelineFold.Resolve([Event(1, GenericEventCodes.EventReversed) with { ReversesEventId = Id(2) }, Event(2, "GOAL", Home)]);

        Statuses(entries).ShouldBe([EffectiveStatus.Active, EffectiveStatus.Active]);
    }

    [Fact]
    public void Two_events_with_one_sequence_are_a_bug()
    {
        Should.Throw<ArgumentException>(() => TimelineFold.Resolve([Event(1, "GOAL", Home), Event(1, "GOAL", Away) with { Id = Id(2) }]));
    }

    private static List<EffectiveStatus> Statuses(IEnumerable<TimelineEntry> entries) => [.. entries.Select(entry => entry.Status)];
}
