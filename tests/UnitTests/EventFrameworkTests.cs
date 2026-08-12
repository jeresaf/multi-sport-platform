using System.Text.Json;
using FluentAssertions;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class EventFrameworkTests
{
    public static TheoryData<string, string> BuiltInDefinitions => new()
    {
        { "Football", FootballRulesetService.GetEventDefinitions() },
        { "Basketball", BasketballRulesetService.GetEventDefinitions() },
        { "Rugby", RugbyRulesetService.GetEventDefinitions() },
        { "Golf", GolfRulesetService.GetEventDefinitions() },
        { "Combat", CombatSportsRulesetService.GetEventDefinitions() },
        { "Athletics", AthleticsRulesetService.GetEventDefinitions() },
        { "Cricket", CricketT20RulesetService.GetEventDefinitions() }
    };

    [Fact]
    public void NormalizeDefinitions_AddsCanonicalFieldsToLegacyEvent()
    {
        var normalized = EventFramework.NormalizeDefinitions(
            "[{\"id\":\"TRY\",\"name\":\"Try\",\"requiresMinute\":true,\"requiresParticipant\":true}]");
        using var document = JsonDocument.Parse(normalized);
        var item = document.RootElement[0];

        item.GetProperty("segmentApplicability")[0].GetString().Should().Be("*");
        item.GetProperty("requiresSecondaryParticipant").GetBoolean().Should().BeFalse();
        item.GetProperty("timingRequirement").GetString().Should().Be("TimestampOrClock");
        item.GetProperty("correctionPolicy").GetString().Should().Be("ApprovalRequired");
        item.GetProperty("validation").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Theory]
    [MemberData(nameof(BuiltInDefinitions))]
    public void BuiltInDefinitions_NormalizeAndParse(string sport, string json)
    {
        var normalized = EventFramework.NormalizeDefinitions(json);
        var definitions = EventFramework.ParseDefinitions(normalized);

        definitions.Should().NotBeEmpty($"{sport} must define events");
        definitions.Should().OnlyHaveUniqueItems(item => item.Code);
    }

    [Fact]
    public void ParseDefinitions_RejectsDuplicateCodesAndInvalidRanges()
    {
        Action duplicate = () => EventFramework.ParseDefinitions(
            "[{\"id\":\"GOAL\",\"name\":\"Goal\"},{\"id\":\"goal\",\"name\":\"Goal again\"}]");
        Action range = () => EventFramework.ParseDefinitions(
            "[{\"id\":\"GOAL\",\"name\":\"Goal\",\"validation\":{\"minimumValue\":2,\"maximumValue\":1}}]");

        duplicate.Should().Throw<ArgumentException>().WithMessage("*duplicated*");
        range.Should().Throw<ArgumentException>().WithMessage("*minimum cannot exceed*");
    }

    [Fact]
    public void ValidatePayload_EnforcesParticipantsMetricSegmentTimingAndRange()
    {
        var definition = EventFramework.ParseDefinitions(
            "[{\"id\":\"SPLIT\",\"name\":\"Split\",\"segmentApplicability\":[\"Lap\"],\"requiresParticipant\":true,\"requiresSecondaryParticipant\":true,\"requiredMetric\":\"split_seconds\",\"timingRequirement\":\"Clock\",\"validation\":{\"minimumValue\":0,\"maximumValue\":60}}]").Single();
        var primary = Guid.NewGuid();
        var secondary = Guid.NewGuid();
        var valid = new EventPayload(primary, secondary, "split_seconds", 10, "id", "Lap 1", "Lap", 10, null);

        EventFramework.ValidatePayload(definition, valid);
        Action missingSecondary = () => EventFramework.ValidatePayload(definition, valid with { SecondaryParticipantId = null });
        Action wrongMetric = () => EventFramework.ValidatePayload(definition, valid with { MetricName = "time" });
        Action wrongSegment = () => EventFramework.ValidatePayload(definition, valid with { SegmentType = "Round", SegmentName = "Round 1" });
        Action missingClock = () => EventFramework.ValidatePayload(definition, valid with { ClockSeconds = null });
        Action outsideRange = () => EventFramework.ValidatePayload(definition, valid with { Value = 61 });

        missingSecondary.Should().Throw<ArgumentException>().WithMessage("*secondary participant*");
        wrongMetric.Should().Throw<ArgumentException>().WithMessage("*requires metric*");
        wrongSegment.Should().Throw<ArgumentException>().WithMessage("*not applicable*");
        missingClock.Should().Throw<ArgumentException>().WithMessage("*requires a contest clock*");
        outsideRange.Should().Throw<ArgumentException>().WithMessage("*must not exceed 60*");
    }

    [Fact]
    public void ParseDefinitions_ExposesProhibitedCorrectionPolicy()
    {
        var definition = EventFramework.ParseDefinitions(
            "[{\"id\":\"FINAL\",\"name\":\"Final result\",\"correctionPolicy\":\"Prohibited\"}]").Single();

        definition.CorrectionPolicy.Should().Be(EventCorrectionPolicy.Prohibited);
    }
}
