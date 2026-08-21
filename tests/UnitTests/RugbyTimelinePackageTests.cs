using FluentAssertions;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class RugbyTimelinePackageTests
{
    private static readonly string[] RequiredEvents =
    [
        "MATCH_START", "PERIOD_START", "PERIOD_END", "TRY", "PENALTY_TRY",
        "CONVERSION_MADE", "CONVERSION_MISSED", "PENALTY_GOAL",
        "PENALTY_MISSED", "DROP_GOAL", "YELLOW_CARD", "RED_CARD",
        "SIN_BIN_START", "SIN_BIN_END", "SUBSTITUTION",
        "TEMPORARY_REPLACEMENT", "INJURY", "TMO_REVIEW", "TMO_DECISION",
        "MATCH_END", "SCRUM", "LINEOUT", "TURNOVER", "PENALTY", "FREE_KICK"
    ];

    [Theory]
    [InlineData(RugbyVariants.Fifteens)]
    [InlineData(RugbyVariants.Sevens)]
    public void Shared_catalogue_covers_both_rugby_variants(string variant)
    {
        RugbyTimelinePackage.Events(variant).Select(item => item.Id)
            .Should().BeEquivalentTo(RequiredEvents);
        RugbyTimelinePackage.Events(variant).Should().OnlyHaveUniqueItems(item => item.Id);
    }

    [Theory]
    [InlineData("TRY", 5)]
    [InlineData("PENALTY_TRY", 7)]
    [InlineData("CONVERSION_MADE", 2)]
    [InlineData("CONVERSION", 2)]
    [InlineData("PENALTY_GOAL", 3)]
    [InlineData("PENALTY_KICK", 3)]
    [InlineData("DROP_GOAL", 3)]
    public void Rugby_scores_have_canonical_regulation_effects(string eventType, decimal points)
    {
        var definition = RugbyTimelinePackage.Find(eventType);
        definition.Should().NotBeNull();
        definition!.ScoreEffect.Should().Be("REGULATION");
        definition.DefaultValue.Should().Be(points);
    }

    [Theory]
    [InlineData("CONVERSION", "CONVERSION_MADE")]
    [InlineData("PENALTY_KICK", "PENALTY_GOAL")]
    [InlineData("SIN_BIN", "SIN_BIN_START")]
    [InlineData("LINE_OUT", "LINEOUT")]
    [InlineData("REFEREE_DECISION", "TMO_DECISION")]
    public void Legacy_event_ids_map_to_canonical_timeline_names(string legacy, string canonical)
    {
        RugbyTimelinePackage.CanonicalType(legacy).Should().Be(canonical);
    }
}
