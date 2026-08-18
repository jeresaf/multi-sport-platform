using FluentAssertions;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class FootballTimelinePackageTests
{
    private static readonly string[] RequiredEvents =
    [
        "MATCH_START", "PERIOD_START", "PERIOD_END", "GOAL", "OWN_GOAL",
        "PENALTY_GOAL", "PENALTY_MISSED", "ASSIST", "YELLOW_CARD",
        "SECOND_YELLOW_CARD", "RED_CARD", "SUBSTITUTION", "PLAYER_IN",
        "PLAYER_OUT", "VAR_REVIEW", "VAR_DECISION", "INJURY", "INJURY_TIME",
        "PENALTY_SHOOTOUT_START", "PENALTY_SHOOTOUT_SCORE",
        "PENALTY_SHOOTOUT_MISS", "MATCH_END", "SHOT", "SHOT_ON_TARGET",
        "SHOT_OFF_TARGET", "BLOCKED_SHOT", "CORNER", "OFFSIDE", "FOUL",
        "FREE_KICK"
    ];

    [Fact]
    public void Catalogue_covers_required_and_detailed_football_events()
    {
        FootballTimelinePackage.Events.Select(item => item.Id)
            .Should().BeEquivalentTo(RequiredEvents);
        FootballTimelinePackage.Events.Should().OnlyHaveUniqueItems(item => item.Id);
        var categories = new HashSet<string>
        {
            "SYSTEM", "PERIOD", "SCORING", "DISCIPLINE", "PERSONNEL",
            "PLAY", "REVIEW", "STOPPAGE"
        };
        var importance = new HashSet<string> { "KEY", "STANDARD", "DETAILED" };
        FootballTimelinePackage.Events.Should().OnlyContain(item =>
            categories.Contains(item.Category));
        FootballTimelinePackage.Events.Should().OnlyContain(item =>
            importance.Contains(item.Importance));
    }

    [Fact]
    public void Scoring_and_shootout_events_have_separate_score_effects()
    {
        FootballTimelinePackage.Find("GOAL")!.ScoreEffect.Should().Be("REGULATION");
        FootballTimelinePackage.Find("PENALTY_GOAL")!.ScoreEffect.Should().Be("REGULATION");
        FootballTimelinePackage.Find("PENALTY_SHOOTOUT_SCORE")!.ScoreEffect
            .Should().Be("SHOOTOUT");
        FootballTimelinePackage.Find("PENALTY_MISSED")!.ScoreEffect.Should().Be("NONE");
    }

    [Fact]
    public void Ruleset_is_parseable_and_preserves_scorer_and_substitution_requirements()
    {
        var definitions = EventFramework.ParseDefinitions(
            FootballRulesetService.GetEventDefinitions());

        definitions.Single(item => item.Code == "GOAL")
            .RequiresSecondaryParticipant.Should().BeFalse("a scorer is optional");
        definitions.Single(item => item.Code == "ASSIST")
            .RequiresSecondaryParticipant.Should().BeTrue();
        definitions.Single(item => item.Code == "SUBSTITUTION")
            .RequiredMetric.Should().Be("substitutions");
        definitions.Should().Contain(item => item.Code == "SECOND_YELLOW",
            "existing scorer clients retain compatibility aliases");
    }
}
