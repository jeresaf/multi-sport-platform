using FluentAssertions;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines;
using SportsPlatform.Modules.FormatEngines.Interfaces;
using Xunit;

namespace SportsPlatform.SportPackageTests;

public sealed class SportPackageRuleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LeaderboardCut_IncludesEveryParticipantTiedAtTheCutLine(bool lowerIsBetter)
    {
        var participants = Participants(5);
        var values = lowerIsBetter
            ? new[] { 70, 71, 72, 72, 80 }
            : new[] { 80, 79, 78, 78, 70 };
        var scores = participants.Select((participant, index) =>
            new RoundScore(participant.ParticipantId, 1, values[index]));
        var options = new MultiRoundLeaderboardOptions
        {
            CutAfterRound = 1,
            CutToPlayers = 3,
            LowerIsBetter = lowerIsBetter
        };

        var qualified = new MultiRoundLeaderboardEngine()
            .ApplyCut(participants, scores, options).ToList();

        qualified.Should().HaveCount(4);
        qualified.Select(item => item.ParticipantId)
            .Should().BeEquivalentTo(participants.Take(4).Select(item => item.ParticipantId));
    }

    [Fact]
    public void HeatQualification_AdvancesPlaceFinishersThenAllTiedAtTheTimeCutoff()
    {
        var results = new[]
        {
            Heat(1, (Id(1), 1, 10.0), (Id(2), 2, 10.5), (Id(3), 3, 12.0)),
            Heat(2, (Id(4), 1, 9.8), (Id(5), 2, 10.5), (Id(6), 3, 11.0))
        };

        var qualification = new HeatQualificationEngine().CalculateQualification(results,
            new HeatQualificationOptions
            {
                QualifyByPlace = 1,
                TotalQualificationSlots = 3,
                AdvanceTies = true
            });

        qualification.QualifiedParticipants.Select(item => item.ParticipantId)
            .Should().BeEquivalentTo(new[] { Id(1), Id(2), Id(4), Id(5) });
        qualification.NotQualifiedParticipants.Select(item => item.ParticipantId)
            .Should().BeEquivalentTo(new[] { Id(3), Id(6) });
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void HeatQualification_PlaceTiePolicyIsConfigurable(
        bool advancePlaceTies,
        int expectedQualifiers)
    {
        var result = new HeatResult(
            Id(50),
            1,
            new[]
            {
                new HeatParticipantResult(Id(1), 4, 1, TimeSpan.FromSeconds(10), false, null),
                new HeatParticipantResult(Id(2), 5, 1, TimeSpan.FromSeconds(10), false, null),
                new HeatParticipantResult(Id(3), 3, 3, null, false, null, true)
            });

        var qualification = new HeatQualificationEngine().CalculateQualification(
            new[] { result },
            new HeatQualificationOptions
            {
                QualifyByPlace = 1,
                TotalQualificationSlots = 1,
                AdvancePlaceTies = advancePlaceTies,
                AdvanceTimeTies = false
            });

        qualification.QualifiedParticipants.Should().HaveCount(expectedQualifiers);
        qualification.NotQualifiedParticipants.Should().Contain(item =>
            item.ParticipantId == Id(3) && item.QualificationReason == "Disqualified");
    }

    [Fact]
    public void BestOfThreeSeries_CompletesOnlyAfterAParticipantWinsTwice()
    {
        var engine = new BestOfSeriesEngine();
        var first = CompletedSeriesGame(1, Id(1));
        engine.DetermineSeriesWinner(new[] { first }).Should().BeNull();

        var second = CompletedSeriesGame(2, Id(2));
        engine.DetermineSeriesWinner(new[] { first, second }).Should().BeNull();

        var third = CompletedSeriesGame(3, Id(1));
        var winner = engine.DetermineSeriesWinner(new[] { first, second, third });
        winner.Should().NotBeNull();
        winner!.WinnerId.Should().Be(Id(1));
        winner.GamesWon.Should().Be(2);
        winner.GamesLost.Should().Be(1);
        winner.WentToFinalGame.Should().BeTrue();
    }

    private static Contest CompletedSeriesGame(int game, Guid winner)
    {
        var contest = new Contest(Id(200 + game), Id(101), Id(102), "SeriesGame");
        contest.AssignToSeries(Id(210), game, 3);
        contest.SetWinner(winner);
        contest.Schedule("Series venue", DateTime.UtcNow.AddHours(game));
        contest.Start();
        contest.Complete();
        return contest;
    }

    private static HeatResult Heat(
        int heatNumber,
        params (Guid ParticipantId, int Place, double Seconds)[] results) =>
        new(Id(300 + heatNumber), heatNumber, results.Select((item, index) =>
            new HeatParticipantResult(
                item.ParticipantId,
                index + 1,
                item.Place,
                TimeSpan.FromSeconds(item.Seconds),
                false,
                null)));

    private static List<ParticipantInfo> Participants(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new ParticipantInfo(Id(index), "Athlete", index))
            .ToList();

    private static Guid Id(int value) =>
        Guid.Parse($"50000000-0000-0000-0000-{value:D12}");
}
