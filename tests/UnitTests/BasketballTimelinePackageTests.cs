using FluentAssertions;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class BasketballTimelinePackageTests
{
    private static readonly string[] RequiredEvents =
    [
        "GAME_START", "PERIOD_START", "PERIOD_END", "FREE_THROW_MADE",
        "FREE_THROW_MISSED", "TWO_POINT_MADE", "TWO_POINT_MISSED",
        "THREE_POINT_MADE", "THREE_POINT_MISSED", "ASSIST",
        "REBOUND_OFFENSIVE", "REBOUND_DEFENSIVE", "STEAL", "BLOCK",
        "TURNOVER", "FOUL_PERSONAL", "FOUL_TECHNICAL",
        "FOUL_UNSPORTSMANLIKE", "FOUL_DISQUALIFYING", "SUBSTITUTION",
        "TIMEOUT", "JUMP_BALL", "GAME_END"
    ];

    [Fact]
    public void Catalogue_covers_required_basketball_play_by_play_events()
    {
        BasketballTimelinePackage.Events.Select(item => item.Id)
            .Should().BeEquivalentTo(RequiredEvents);
        BasketballTimelinePackage.Events.Should().OnlyHaveUniqueItems(item => item.Id);
    }

    [Theory]
    [InlineData("FREE_THROW_MADE", 1)]
    [InlineData("TWO_POINT_MADE", 2)]
    [InlineData("THREE_POINT_MADE", 3)]
    [InlineData("FIELD_GOAL_MADE", 2)]
    [InlineData("THREE_POINTER_MADE", 3)]
    public void Made_shots_have_regulation_score_effect(string eventType, decimal points)
    {
        var definition = BasketballTimelinePackage.Find(eventType);
        definition.Should().NotBeNull();
        definition!.ScoreEffect.Should().Be("REGULATION");
        definition.DefaultValue.Should().Be(points);
    }

    [Theory]
    [InlineData("FIELD_GOAL_MADE", "TWO_POINT_MADE")]
    [InlineData("THREE_POINTER_MADE", "THREE_POINT_MADE")]
    [InlineData("OFFENSIVE_REBOUND", "REBOUND_OFFENSIVE")]
    [InlineData("TECHNICAL_FOUL", "FOUL_TECHNICAL")]
    [InlineData("SUBSTITUTION_IN", "SUBSTITUTION")]
    public void Legacy_event_ids_have_canonical_timeline_names(string legacy, string canonical)
    {
        BasketballTimelinePackage.CanonicalType(legacy).Should().Be(canonical);
    }
}
