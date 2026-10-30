using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using static SportsPlatform.UnitTests.Engines.EngineTestData;

namespace SportsPlatform.UnitTests.Engines;

public sealed class PersonnelTrackerTests
{
    private static readonly Guid _starter = Person(1);
    private static readonly Guid _otherStarter = Person(2);
    private static readonly Guid _substitute = Person(12);
    private static readonly Guid _coach = Person(30);

    private static readonly ContestLineup _home = new(Home, [_starter, _otherStarter], [_substitute]) { Staff = [_coach] };

    [Fact]
    public void The_lineup_starts_on_the_field_the_bench_and_the_staff()
    {
        var home = Track(Football);

        home.OnField.ShouldBe([_starter, _otherStarter], ignoreOrder: true);
        home.Bench.ShouldBe([_substitute]);
        home.Staff.ShouldBe([_coach]);
    }

    [Fact]
    public void A_substitution_replaces_the_player_off_with_the_player_on()
    {
        var home = Track(Football, Event(1, "SUBSTITUTION", Home) with { Actors = Actors(("PLAYER_OFF", _starter), ("PLAYER_ON", _substitute)) });

        home.OnField.ShouldBe([_otherStarter, _substitute], ignoreOrder: true);
        home.Bench.ShouldBeEmpty();
        home.Replaced.ShouldBe([_starter]);
    }

    [Fact]
    public void A_send_off_dismisses_a_player_or_a_member_of_staff()
    {
        var home = Track(
            Football,
            Event(1, "RED_CARD", Home) with { Actors = Actors(("OFFENDER", _starter)) },
            Event(2, "SECOND_YELLOW_CARD", Home) with { Actors = Actors(("OFFENDER", _coach)) });

        home.Dismissed.ShouldBe([_starter, _coach], ignoreOrder: true);
        home.OnField.ShouldBe([_otherStarter]);
        home.Staff.ShouldBeEmpty();
    }

    [Fact]
    public void Events_without_effects_move_nobody()
    {
        var home = Track(Football, Event(1, "YELLOW_CARD", Home) with { Actors = Actors(("OFFENDER", _starter)) });

        home.OnField.ShouldContain(_starter);
        home.Dismissed.ShouldBeEmpty();
    }

    [Fact]
    public void A_suspended_player_is_off_the_field_until_they_return()
    {
        var suspended = Track(RugbyUnion15, Event(1, "YELLOW_CARD", Home) with { Actors = Actors(("OFFENDER", _starter)) });
        var returned = Track(
            RugbyUnion15,
            Event(1, "YELLOW_CARD", Home) with { Actors = Actors(("OFFENDER", _starter)) },
            Event(2, "SIN_BIN_RETURN", Home) with { Actors = Actors(("OFFENDER", _starter)) });

        suspended.Suspended.ShouldBe([_starter]);
        suspended.OnField.ShouldNotContain(_starter);
        returned.Suspended.ShouldBeEmpty();
        returned.OnField.ShouldContain(_starter);
    }

    [Fact]
    public void After_a_temporary_substitution_the_player_returns_and_the_replacement_goes_back_to_the_bench()
    {
        var replaced = Track(RugbyUnion15, Event(1, "TEMPORARY_REPLACEMENT", Home) with { Actors = Actors(("PLAYER_OFF", _starter), ("PLAYER_ON", _substitute)) });
        var returned = Track(
            RugbyUnion15,
            Event(1, "TEMPORARY_REPLACEMENT", Home) with { Actors = Actors(("PLAYER_OFF", _starter), ("PLAYER_ON", _substitute)) },
            Event(2, "TEMPORARY_REPLACEMENT_END", Home) with { Actors = Actors(("PLAYER_ON", _substitute)) });

        replaced.TemporarilyReplaced.ShouldBe(new Dictionary<Guid, Guid> { [_starter] = _substitute });
        replaced.OnField.ShouldBe([_otherStarter, _substitute], ignoreOrder: true);
        returned.TemporarilyReplaced.ShouldBeEmpty();
        returned.OnField.ShouldBe([_starter, _otherStarter], ignoreOrder: true);
        returned.Bench.ShouldBe([_substitute]);
    }

    [Fact]
    public void Without_a_lineup_people_appear_where_events_put_them()
    {
        var home = PersonnelTracker.Track(Context(Football), [Event(1, "RED_CARD", Home) with { Actors = Actors(("OFFENDER", _starter)) }]).For(Home);

        home.Dismissed.ShouldBe([_starter]);
        home.OnField.ShouldBeEmpty();
    }

    private static ParticipantPersonnel Track(Modules.SportsConfiguration.Contracts.Sports.SportDefinition definition, params TimelineEvent[] events) =>
        PersonnelTracker.Track(Context(definition, _home), events).For(Home);

    private static Dictionary<string, Guid> Actors(params (string Role, Guid Person)[] actors) =>
        actors.ToDictionary(actor => actor.Role, actor => actor.Person);
}
