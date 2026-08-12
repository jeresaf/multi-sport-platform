using FluentAssertions;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class CompetitionHierarchyTests
{
    [Fact]
    public void Competition_SupportsMultipleEditionsAndNestedStageStructures()
    {
        var tenantId = Guid.NewGuid();
        var competition = new Competition(Guid.NewGuid(), tenantId, "National League", Guid.NewGuid());
        var first = competition.AddEdition("2026", new DateTime(2026, 1, 1), new DateTime(2026, 6, 30));
        competition.AddEdition("2027", new DateTime(2027, 1, 1), null);
        var stage = first.AddStage("Championship", StageType.GroupAndKnockout, 1);
        var group = stage.AddGroup("Group A");
        var round = stage.AddRound("Round 1", 1);

        competition.Editions.Should().HaveCount(2);
        first.Stages.Should().ContainSingle().Which.Should().BeSameAs(stage);
        stage.Groups.Should().ContainSingle().Which.Should().BeSameAs(group);
        stage.Rounds.Should().ContainSingle().Which.Should().BeSameAs(round);
    }

    [Fact]
    public void ManualContest_CanBePlacedAtStageGroupAndRound()
    {
        var tenantId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var contest = new Contest(Guid.NewGuid(), tenantId, Guid.NewGuid(), "HeadToHead");

        contest.AssignToHierarchy(stageId, groupId, roundId, 3);

        contest.StageId.Should().Be(stageId);
        contest.GroupId.Should().Be(groupId);
        contest.RoundId.Should().Be(roundId);
        contest.SortOrder.Should().Be(3);
        var invalidEdition = () => new Competition(Guid.NewGuid(), tenantId, "League", Guid.NewGuid())
            .AddEdition("Invalid", new DateTime(2026, 2, 1), new DateTime(2026, 1, 1));
        invalidEdition.Should().Throw<ArgumentException>();
    }
}
