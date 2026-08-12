using FluentAssertions;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class UniversalContestModelTests
{
    [Theory]
    [InlineData("TeamVsTeam", ContestShapeKind.HeadToHeadTeam)]
    [InlineData("HeadToHeadIndividual", ContestShapeKind.HeadToHeadIndividual)]
    [InlineData("MultiRoundLeaderboard", ContestShapeKind.MultiParticipantRanked)]
    [InlineData("Race", ContestShapeKind.Race)]
    [InlineData("Heat", ContestShapeKind.Heat)]
    [InlineData("BestOfSeries", ContestShapeKind.BestOfSeries)]
    [InlineData("Bout", ContestShapeKind.FightCardBout)]
    [InlineData("CricketGroupMatch", ContestShapeKind.HeadToHeadTeam)]
    public void ShapeCatalog_ClassifiesEveryUniversalAndPrototypeShape(
        string shape, ContestShapeKind expected)
    {
        new Contest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), shape)
            .ShapeKind.Should().Be(expected);
    }

    [Fact]
    public void Contest_StoresEveryUniversalAttributeWithoutSportSpecificRedesign()
    {
        var tenantId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var contest = new Contest(Guid.NewGuid(), tenantId, Guid.NewGuid(), "HeadToHeadIndividual");
        contest.SetName("Championship final");
        contest.AddParticipant(participantId, "Athlete");
        var segment = contest.AddSegment("Round 1", "Round", 1);
        var contestEvent = contest.AddEvent(
            "SCORE", participantId, segment.Id, "10", Guid.NewGuid(),
            "device", 1, Guid.NewGuid().ToString("N"));
        contest.RecordMetric("points", 10, null, "pts", participantId, segment.Id);
        contest.AddOfficial(Guid.NewGuid(), "Referee", "Match official");
        contest.Schedule("Main arena", DateTime.UtcNow.AddHours(1), 60);
        var result = new ContestResult(Guid.NewGuid(), contest.Id, contestEvent.Id, participantId);
        result.SetScore(10, "10", null);

        contest.Venue.Should().Be("Main arena");
        contest.StartTime.Should().NotBeNull();
        contest.Participants.Should().ContainSingle();
        contest.Segments.Should().ContainSingle();
        contest.Events.Should().ContainSingle();
        contest.Metrics.Should().ContainSingle();
        contest.Officials.Should().ContainSingle();
        result.ContestId.Should().Be(contest.Id);
        contest.Status.Should().Be(ContestStatus.Scheduled);
    }

    [Fact]
    public void SeriesAssignment_ReclassifiesAHeadToHeadContestAsBestOfSeries()
    {
        var contest = new Contest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HeadToHead");
        contest.AssignToSeries(Guid.NewGuid(), 1, 5);
        contest.ShapeKind.Should().Be(ContestShapeKind.BestOfSeries);
    }
}
