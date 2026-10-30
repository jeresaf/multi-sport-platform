using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// The API's definition DTO (S05 D16) keeps every entry of the resolved definition.
public sealed class SportDefinitionResponseTests
{
    private static readonly SportDefinition _football = SportDefinitionContent.Read(YamlJson.Parse(new SportDefinitionFiles().Read("football.yaml"))!) with
    {
        GenericEventTypes = GenericEventTypes.All,
        ContentHash = "sha256:0123",
        PublishedAt = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Every_list_and_map_keeps_its_entries_in_order()
    {
        var response = SportDefinitionResponse.From(_football);

        response.ShouldSatisfyAllConditions(
            r => r.Variant.ShouldBe(_football.Variant),
            r => r.Version.ShouldBe(_football.Version),
            r => r.ContentHash.ShouldBe("sha256:0123"),
            r => r.PublishedAt.ShouldBe(_football.PublishedAt),
            r => r.Sides.ShouldBe(_football.Sides),
            r => r.TieResolutions.ShouldBe(_football.Supports.TieResolutions),
            r => r.Periods.Select(p => (p.Code, p.ShortLabel)).ShouldBe(_football.Periods.Select(p => (p.Code, p.ShortLabel))),
            r => r.ActorRoles.Keys.ShouldBe(_football.ActorRoles.Keys, ignoreOrder: true),
            r => r.EventTypes.Select(e => e.Code).ShouldBe(_football.EventTypes.Select(e => e.Code)),
            r => r.GenericEventTypes.Select(e => e.Code).ShouldBe(GenericEventTypes.All.Select(e => e.Code)),
            r => r.Positions.Select(p => p.Code).ShouldBe(_football.Positions.Select(p => p.Code)),
            r => r.StaffRoles.Select(s => s.Code).ShouldBe(_football.StaffRoles.Select(s => s.Code)),
            r => r.OfficialRoles.Select(o => o.Code).ShouldBe(_football.OfficialRoles.Select(o => o.Code)),
            r => r.Lineup.PositionConstraints.Select(c => c.Group).ShouldBe(_football.Lineup.PositionConstraints.Select(c => c.Group)),
            r => r.Statistics.Select(s => s.Code).ShouldBe(_football.Statistics.Select(s => s.Code)),
            r => r.ScorerPad.Groups.Select(g => g.Buttons.Count).ShouldBe(_football.ScorerPad.Groups.Select(g => g.Buttons.Count)));
    }

    [Fact]
    public void An_event_type_keeps_its_actors_metadata_follow_up_limits_and_reversals()
    {
        var response = SportDefinitionResponse.From(_football);

        foreach (var (type, mapped) in _football.EventTypes.Zip(response.EventTypes))
        {
            mapped.Actors.Select(a => a.Role).ShouldBe(type.Actors.Select(a => a.Role));
            mapped.Metadata.Keys.ShouldBe(type.Metadata.Keys, ignoreOrder: true);
            mapped.FollowUp?.RequiresPreceding.ShouldBe(type.FollowUp!.RequiresPreceding);
            (mapped.FollowUp is null).ShouldBe(type.FollowUp is null);
            mapped.Limits.ShouldBe(type.Limits.Select(l => l.Rule));
            mapped.ReversibleBy.ShouldBe(type.ReversibleBy);
            mapped.Effects.ShouldBe(type.Effects);
        }

        var goal = response.EventTypes.Single(e => e.Code == "GOAL");
        goal.Metadata["body_part"].ShouldBe(
            new SportMetadataFieldResponse(SportMetadataFieldType.Choice, goal.Metadata["body_part"].Values, null, null, Optional: true));
        goal.Metadata["body_part"].Values.ShouldBe(["RIGHT_FOOT", "LEFT_FOOT", "HEAD", "OTHER"]);
    }

    // R2 (#77): positional records with neighbouring arguments of one type; a swap would compile, so assert values.
    [Fact]
    public void Football_values_reach_their_own_fields()
    {
        var response = SportDefinitionResponse.From(_football);

        response.Periods[0].ShouldBe(new SportPeriodResponse(
            "FIRST_HALF", "1st half", "1H", PhaseKind.Play, PeriodClock.CountUp, DefaultSeconds: 2700, ClockStartsAtSeconds: 0,
            AllowsAddedTime: true, Overtime: false, Repeatable: false, SuddenDeath: false));
        response.Periods[1].DefaultSeconds.ShouldBe(900);
        response.Periods[2].ClockStartsAtSeconds.ShouldBe(2700);
        response.OfficialRoles[1].ShouldBe(new SportOfficialRoleResponse("ASSISTANT_REFEREE", "Assistant referee", DefaultMinimum: 0, Max: 2));
        response.Lineup.ShouldSatisfyAllConditions(
            l => l.Starters.ShouldBe(11),
            l => l.BenchMax.ShouldBe(9),
            l => l.PositionConstraints.ShouldBe([new SportPositionConstraintResponse("GOALKEEPER", Min: 1, Max: 1)]),
            l => l.SpecialRoles.ShouldBe(["CAPTAIN"]),
            l => l.MinimumPlayersToContinue.ShouldBe(7),
            l => l.ShirtNumbersFollowPositions.ShouldBeFalse());
        var shotsOnTarget = response.Statistics.Single(s => s.Code == "SHOTS_ON_TARGET");
        shotsOnTarget.Derive.CountEvents.ShouldBe(["SHOT"]);
        shotsOnTarget.Derive.ActorRoles.ShouldBe(["PLAYER"]);
        shotsOnTarget.Derive.Where.ShouldBe(new Dictionary<string, string> { ["on_target"] = "true" });
        response.Statistics.Single(s => s.Code == "PENALTIES_MISSED").HigherIsBetter.ShouldBeFalse();
    }

    // Each flag set alone, and every number distinct, so a swapped argument changes the result.
    [Fact]
    public void Each_flag_and_number_reaches_its_own_field()
    {
        var periods = new[]
        {
            Period("A") with { AllowsAddedTime = true, DefaultSeconds = 11, ClockStartsAtSeconds = 12 },
            Period("B") with { Overtime = true },
            Period("C") with { Repeatable = true },
            Period("D") with { SuddenDeath = true },
        };
        var followUps = new[]
        {
            new FollowUpDefinition(["A"]) { SameSide = true },
            new FollowUpDefinition(["B"]) { SamePeriod = true },
            new FollowUpDefinition(["C"]) { Unconsumed = true },
            new FollowUpDefinition(["D"]) { Consumes = true },
        };
        var template = _football.EventTypes[0];
        var definition = _football with
        {
            Periods = periods,
            EventTypes = [.. followUps.Select((followUp, index) => template with
            {
                Code = $"E{index}",
                FollowUp = followUp,
                Actors = [new EventActorDefinition("R") { RequiredFrom = CoverageLevel.Standard, Optional = true, DistinctFrom = "S" }],
                Metadata = new Dictionary<string, MetadataFieldDefinition> { ["m"] = new(MetadataFieldType.Number) { Min = 3, Max = 7 } },
            })],
            Lineup = _football.Lineup with { PositionConstraints = [new PositionConstraintDefinition("BACK") { Min = 2, Max = 5 }] },
            Statistics = [new StatisticDefinition("PCT", "Pct", ["TEAM"], StatisticAggregation.Ratio, true, CoverageLevel.Detailed, new StatisticDerivation
            {
                Builtin = "RATIO",
                Numerator = "MADE",
                Denominator = "ATTEMPTED",
            })],
        };

        var response = SportDefinitionResponse.From(definition);

        response.Periods.Select(p => (p.AllowsAddedTime, p.Overtime, p.Repeatable, p.SuddenDeath)).ShouldBe(
            [(true, false, false, false), (false, true, false, false), (false, false, true, false), (false, false, false, true)]);
        (response.Periods[0].DefaultSeconds, response.Periods[0].ClockStartsAtSeconds).ShouldBe((11, 12));
        response.EventTypes.Select(e => (e.FollowUp!.SameSide, e.FollowUp.SamePeriod, e.FollowUp.Unconsumed, e.FollowUp.Consumes)).ShouldBe(
            [(true, false, false, false), (false, true, false, false), (false, false, true, false), (false, false, false, true)]);
        response.EventTypes.Select(e => e.FollowUp!.RequiresPreceding.Single()).ShouldBe(["A", "B", "C", "D"]);
        response.EventTypes[0].Actors.ShouldBe([new SportEventActorResponse("R", CoverageLevel.Standard, Optional: true, DistinctFrom: "S")]);
        var field = response.EventTypes[0].Metadata["m"];
        (field.Type, field.Min, field.Max, field.Optional).ShouldBe((SportMetadataFieldType.Number, 3L, 7L, false));
        response.Lineup.PositionConstraints.ShouldBe([new SportPositionConstraintResponse("BACK", Min: 2, Max: 5)]);
        var derive = response.Statistics.ShouldHaveSingleItem().Derive;
        (derive.Builtin, derive.Numerator, derive.Denominator, derive.Manual).ShouldBe(("RATIO", "MADE", "ATTEMPTED", false));
    }

    private static PeriodDefinition Period(string code) => new(code, code, code, PhaseKind.Play, PeriodClock.CountDown);

    [Theory]
    [InlineData(MetadataFieldType.Choice, SportMetadataFieldType.Choice)]
    [InlineData(MetadataFieldType.Flag, SportMetadataFieldType.Flag)]
    [InlineData(MetadataFieldType.Number, SportMetadataFieldType.Number)]
    [InlineData(MetadataFieldType.Text, SportMetadataFieldType.Text)]
    public void Metadata_types_have_the_APIs_names(MetadataFieldType file, SportMetadataFieldType api)
    {
        SportMetadataFieldResponse.From(new MetadataFieldDefinition(file)).Type.ShouldBe(api);
    }
}
