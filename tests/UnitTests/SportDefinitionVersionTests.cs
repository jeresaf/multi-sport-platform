using FluentAssertions;
using SportsPlatform.Modules.Sports.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class SportDefinitionVersionTests
{
    [Fact]
    public void DraftVersion_RequiresCompleteValidConfigurationBeforePublishing()
    {
        var version = new SportDefinitionVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, "Draft", "test");

        var publish = version.Invoking(item => item.Publish());
        publish.Should().Throw<InvalidOperationException>()
            .WithMessage("*Configure every required*");
        version.Invoking(item => item.ConfigureContestShape(
                "TeamVsTeam", "Team", "{}", "[]", "[]", "[]", "[]", "{}", "{}"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*JSON array*");
    }

    [Fact]
    public void CloneVersion_CopiesEveryDefinitionFieldIntoANewEditableDraft()
    {
        var sport = new Sport(Guid.NewGuid(), "RUGBY", "Rugby");
        var source = sport.CreateNewVersion("Rugby v1", "test");
        source.ConfigureContestShape(
            "TeamVsTeam", "Team", "[{\"type\":\"half\"}]", "[{\"id\":\"TRY\",\"name\":\"Try\"}]",
            "[{\"code\":\"points\",\"name\":\"Points\",\"type\":\"Integer\",\"validation\":{\"minimum\":0}}]", "[\"winner\"]", "[{\"order\":\"points\"}]",
            "{\"scoreboard\":true}", "{\"maxPlayers\":15}");
        source.Publish();

        var clone = sport.CloneVersion(source.Id, "Rugby v2", "other-user");

        clone.Id.Should().NotBe(source.Id);
        clone.VersionNumber.Should().Be(2);
        clone.IsPublished.Should().BeFalse();
        clone.ContestShape.Should().Be(source.ContestShape);
        clone.ParticipantType.Should().Be(source.ParticipantType);
        clone.SegmentDefinitions.Should().Be(source.SegmentDefinitions);
        clone.EventDefinitions.Should().Be(source.EventDefinitions);
        clone.MetricDefinitions.Should().Be(source.MetricDefinitions);
        clone.ResultMethods.Should().Be(source.ResultMethods);
        clone.RankingRules.Should().Be(source.RankingRules);
        clone.DisplayConfiguration.Should().Be(source.DisplayConfiguration);
        clone.ValidationConfiguration.Should().Be(source.ValidationConfiguration);
        clone.Invoking(item => item.ConfigureContestShape(
                "TeamVsTeam", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}"))
            .Should().NotThrow();
    }

    [Fact]
    public void PublishedVersion_RejectsConfigurationChanges()
    {
        var version = new SportDefinitionVersion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Rugby rules",
            "test");
        version.ConfigureContestShape(
            "TeamVsTeam",
            "Team",
            "[]",
            "[]",
            "[]",
            "[]",
            "[]",
            "{}",
            "{}");
        version.Publish();

        var change = () => version.ConfigureContestShape(
            "Individual",
            "Athlete",
            "[{}]",
            "[{}]",
            "[{}]",
            "[{}]",
            "[{}]",
            "{\"changed\":true}",
            "{\"changed\":true}");

        change.Should().Throw<InvalidOperationException>()
            .WithMessage("*published sport definition version cannot be changed*");
        version.ContestShape.Should().Be("TeamVsTeam");
        version.ParticipantType.Should().Be("Team");
    }
}
