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
            new MetadataFieldResponse(MetadataValueType.Choice, goal.Metadata["body_part"].Values, null, null, Optional: true));
        goal.Metadata["body_part"].Values.ShouldBe(["RIGHT_FOOT", "LEFT_FOOT", "HEAD", "OTHER"]);
    }

    [Theory]
    [InlineData(MetadataFieldType.Choice, MetadataValueType.Choice)]
    [InlineData(MetadataFieldType.Flag, MetadataValueType.Flag)]
    [InlineData(MetadataFieldType.Number, MetadataValueType.Number)]
    [InlineData(MetadataFieldType.Text, MetadataValueType.Text)]
    public void Metadata_types_have_the_APIs_names(MetadataFieldType file, MetadataValueType api)
    {
        MetadataFieldResponse.From(new MetadataFieldDefinition(file)).Type.ShouldBe(api);
    }
}
