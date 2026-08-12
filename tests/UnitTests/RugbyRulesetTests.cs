using System.Text.Json;
using FluentAssertions;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class RugbyRulesetTests
{
    [Theory]
    [InlineData("7s", RugbyVariants.Sevens)]
    [InlineData("sevens", RugbyVariants.Sevens)]
    [InlineData("15s", RugbyVariants.Fifteens)]
    [InlineData("fifteens", RugbyVariants.Fifteens)]
    public void Normalize_AcceptsPublicRugbyFormatNames(string value, string expected)
    {
        RugbyVariants.Normalize(value).Should().Be(expected);
    }

    [Fact]
    public void Fifteens_DefinesFullLengthTeamAndBonusPointRules()
    {
        using var segments = JsonDocument.Parse(
            RugbyRulesetService.GetSegmentDefinitions(RugbyVariants.Fifteens));
        using var validation = JsonDocument.Parse(
            RugbyRulesetService.GetValidationConfiguration(RugbyVariants.Fifteens));

        segments.RootElement[0].GetProperty("durationMinutes").GetInt32().Should().Be(40);
        validation.RootElement.GetProperty("playersPerSide").GetInt32().Should().Be(15);
        validation.RootElement.GetProperty("timing").GetProperty("sinBinMinutes")
            .GetInt32().Should().Be(10);
        validation.RootElement.GetProperty("standings").GetProperty("winPoints")
            .GetInt32().Should().Be(4);
        validation.RootElement.GetProperty("standings").GetProperty("useTryBonus")
            .GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Sevens_DefinesTournamentTimingRosterDisciplineAndPoolPoints()
    {
        using var segments = JsonDocument.Parse(
            RugbyRulesetService.GetSegmentDefinitions(RugbyVariants.Sevens));
        using var validation = JsonDocument.Parse(
            RugbyRulesetService.GetValidationConfiguration(RugbyVariants.Sevens));
        using var events = JsonDocument.Parse(
            RugbyRulesetService.GetEventDefinitions(RugbyVariants.Sevens));

        segments.RootElement[0].GetProperty("durationMinutes").GetInt32().Should().Be(7);
        segments.RootElement[1].GetProperty("durationMinutes").GetInt32().Should().Be(2);
        validation.RootElement.GetProperty("playersPerSide").GetInt32().Should().Be(7);
        validation.RootElement.GetProperty("participants").GetProperty("maxRosterSize")
            .GetInt32().Should().Be(13);
        validation.RootElement.GetProperty("standings").GetProperty("lossPoints")
            .GetInt32().Should().Be(1);
        validation.RootElement.GetProperty("standings").GetProperty("useTryBonus")
            .GetBoolean().Should().BeFalse();

        events.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "SIN_BIN")
            .GetProperty("validation").GetProperty("sinBinMinutes")
            .GetInt32().Should().Be(2);
        events.RootElement.EnumerateArray().Should().OnlyContain(item =>
            item.GetProperty("validation").GetProperty("maxMinute").GetInt32() == 30);
    }

    [Theory]
    [InlineData(RugbyVariants.Fifteens, 5, 2, 7, 3, 3)]
    [InlineData(RugbyVariants.Sevens, 5, 2, 7, 3, 3)]
    public void BothFormats_UseRugbyUnionScoring(
        string variant,
        int tryPoints,
        int conversionPoints,
        int penaltyTryPoints,
        int penaltyPoints,
        int dropGoalPoints)
    {
        using var validation = JsonDocument.Parse(
            RugbyRulesetService.GetValidationConfiguration(variant));
        var scoring = validation.RootElement.GetProperty("scoring");

        scoring.GetProperty("tryPoints").GetInt32().Should().Be(tryPoints);
        scoring.GetProperty("conversionPoints").GetInt32().Should().Be(conversionPoints);
        scoring.GetProperty("penaltyTryPoints").GetInt32().Should().Be(penaltyTryPoints);
        scoring.GetProperty("penaltyKickPoints").GetInt32().Should().Be(penaltyPoints);
        scoring.GetProperty("dropGoalPoints").GetInt32().Should().Be(dropGoalPoints);
    }
}
