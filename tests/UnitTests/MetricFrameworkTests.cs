using System.Text.Json;
using FluentAssertions;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class MetricFrameworkTests
{
    public static TheoryData<string, string> BuiltInDefinitions => new()
    {
        { "Football", FootballRulesetService.GetMetricDefinitions() },
        { "Basketball", BasketballRulesetService.GetMetricDefinitions() },
        { "Rugby", RugbyRulesetService.GetMetricDefinitions() },
        { "Golf", GolfRulesetService.GetMetricDefinitions() },
        { "Combat", CombatSportsRulesetService.GetMetricDefinitions() },
        { "Athletics", AthleticsRulesetService.GetMetricDefinitions() },
        { "Cricket", CricketT20RulesetService.GetMetricDefinitions() }
    };

    public static TheoryData<string, string, string, string?, int> SupportedValues => new()
    {
        { Definition("Integer", validation: "\"minimum\":0,\"maximum\":10"), "7", "7", null, 0 },
        { Definition("Decimal", "kg", 2), "7.126", "7.13", "kg", 2 },
        { Definition("Duration", "s", 3), "00:01:02.3456", "62.346", "s", 3 },
        { Definition("Clock time", precision: 2), "13:05:07.25", "13:05:07.25", null, 2 },
        { Definition("Distance", "m", 3), "9.87654", "9.877", "m", 3 },
        { Definition("Weight", "kg", 2), "81.255", "81.26", "kg", 2 },
        { Definition("Boolean"), "TRUE", "true", null, 0 },
        { Definition("Text", validation: "\"maximumLength\":12,\"pattern\":\"^[A-Z]+$\""), "VALID", "VALID", null, 0 },
        { Definition("Enumeration", validation: "\"allowedValues\":[\"Gold\",\"Silver\",\"Bronze\"]"), "silver", "Silver", null, 0 },
        { Definition("Composite score", precision: 0, validation: "\"components\":[\"runs\",\"wickets\"]"), "{\"runs\":185,\"wickets\":6}", "{\"runs\":185,\"wickets\":6}", null, 0 }
    };

    [Theory]
    [MemberData(nameof(SupportedValues))]
    public void ValidateValue_SupportsEveryRequiredType(
        string definitionJson,
        string rawValue,
        string expected,
        string? expectedUnit,
        int expectedPrecision)
    {
        var definition = MetricFramework.ParseDefinitions($"[{definitionJson}]").Single();

        var value = MetricFramework.ValidateValue(definition, rawValue, expectedUnit);

        value.SerializedValue.Should().Be(expected);
        value.Unit.Should().Be(expectedUnit);
        value.Precision.Should().Be(expectedPrecision);
    }

    [Fact]
    public void NormalizeDefinitions_AddsValidationAndRankingDefaultsToLegacyMetrics()
    {
        var normalized = MetricFramework.NormalizeDefinitions(
            "[{\"id\":\"FINISH_TIME\",\"name\":\"Finish time\",\"type\":\"Duration\",\"aggregation\":\"Min\",\"unit\":\"s\"}]");
        using var document = JsonDocument.Parse(normalized);
        var metric = document.RootElement[0];

        metric.GetProperty("validation").GetProperty("precision").GetInt32().Should().Be(3);
        metric.GetProperty("sortDirection").GetString().Should().Be("Ascending");
        metric.GetProperty("unit").GetString().Should().Be("s");
    }

    [Theory]
    [MemberData(nameof(BuiltInDefinitions))]
    public void BuiltInSportDefinitions_NormalizeIntoValidatedMetrics(string sport, string json)
    {
        var normalized = MetricFramework.NormalizeDefinitions(json);
        var definitions = MetricFramework.ParseDefinitions(normalized);
        using var document = JsonDocument.Parse(normalized);

        definitions.Should().NotBeEmpty($"{sport} must define metrics");
        foreach (var metric in document.RootElement.EnumerateArray())
        {
            metric.TryGetProperty("validation", out var validation).Should().BeTrue();
            validation.ValueKind.Should().Be(JsonValueKind.Object);
            validation.TryGetProperty("precision", out var precision).Should().BeTrue();
            precision.ValueKind.Should().Be(JsonValueKind.Number);
        }
    }

    [Fact]
    public void ParseDefinitions_RejectsInvalidDefinitions()
    {
        var action = () => MetricFramework.ParseDefinitions(
            "[{\"id\":\"MEDAL\",\"name\":\"Medal\",\"type\":\"Enumeration\",\"validation\":{}}]");

        action.Should().Throw<ArgumentException>().WithMessage("*requires allowed values*");
    }

    [Fact]
    public void ValidateValue_EnforcesRangeUnitAndPrecision()
    {
        var definition = MetricFramework.ParseDefinitions(
            $"[{Definition("Distance", "m", 2, "\"minimum\":1,\"maximum\":10")}]" ).Single();

        MetricFramework.ValidateValue(definition, "9.999", "m").SerializedValue.Should().Be("10.00");
        Action outsideRange = () => MetricFramework.ValidateValue(definition, "11", "m");
        Action wrongUnit = () => MetricFramework.ValidateValue(definition, "5", "ft");
        outsideRange.Should().Throw<ArgumentException>().WithMessage("*must not exceed 10*");
        wrongUnit.Should().Throw<ArgumentException>().WithMessage("*requires unit 'm'*");
    }

    [Fact]
    public void Rank_OrdersApplicableValuesAndUsesCompetitionTies()
    {
        var definition = MetricFramework.ParseDefinitions(
            "[{\"id\":\"TIME\",\"name\":\"Time\",\"type\":\"Duration\",\"unit\":\"s\",\"sortDirection\":\"Ascending\",\"validation\":{\"precision\":3}}]").Single();
        var first = Guid.NewGuid();
        var tied = Guid.NewGuid();
        var third = Guid.NewGuid();

        var ranking = MetricFramework.Rank(definition, new[]
        {
            new MetricRankCandidate(third, "11.200", "s"),
            new MetricRankCandidate(first, "10.125", "s"),
            new MetricRankCandidate(tied, "10.125", "s")
        });

        ranking.Select(item => item.Rank).Should().Equal(1, 1, 3);
        ranking.Take(2).Select(item => item.ParticipantId).Should().BeEquivalentTo(new[] { first, tied });
        ranking[2].ParticipantId.Should().Be(third);
    }

    [Fact]
    public void Rank_OrdersCompositeScoresLexicographically()
    {
        var definition = MetricFramework.ParseDefinitions(
            "[{\"id\":\"SETS\",\"name\":\"Sets and points\",\"type\":\"CompositeScore\",\"sortDirection\":\"Descending\",\"validation\":{\"precision\":0,\"components\":[\"sets\",\"points\"]}}]").Single();
        var winner = Guid.NewGuid();
        var runnerUp = Guid.NewGuid();

        var ranking = MetricFramework.Rank(definition, new[]
        {
            new MetricRankCandidate(runnerUp, "{\"sets\":2,\"points\":21}"),
            new MetricRankCandidate(winner, "{\"sets\":3,\"points\":18}")
        });

        ranking.Select(item => item.ParticipantId).Should().Equal(winner, runnerUp);
        ranking.Select(item => item.Rank).Should().Equal(1, 2);
    }

    private static string Definition(
        string type,
        string? unit = null,
        int? precision = null,
        string? validation = null)
    {
        var unitJson = unit is null ? string.Empty : $",\"unit\":\"{unit}\"";
        var pieces = new List<string>();
        if (precision is not null) pieces.Add($"\"precision\":{precision}");
        if (validation is not null) pieces.Add(validation);
        return $"{{\"id\":\"VALUE\",\"name\":\"Value\",\"type\":\"{type}\"{unitJson},\"validation\":{{{string.Join(',', pieces)}}}}}";
    }
}
