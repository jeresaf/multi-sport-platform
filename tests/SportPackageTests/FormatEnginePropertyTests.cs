using FluentAssertions;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines;
using SportsPlatform.Modules.FormatEngines.Interfaces;
using Xunit;

namespace SportsPlatform.SportPackageTests;

public sealed class FormatEnginePropertyTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SportId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public void RoundRobin_GeneratesEveryPairExactlyOnce_ForParticipantCountsTwoThroughThirtyTwo()
    {
        var engine = new RoundRobinEngine();

        for (var count = 2; count <= 32; count++)
        {
            var participants = Participants(count);
            var contests = engine.GenerateContests(
                Stage(StageType.League), participants,
                new RoundRobinOptions { SportId = SportId, HomeAndAway = false }).ToList();

            contests.Should().HaveCount(count * (count - 1) / 2, $"{count} participants have n(n-1)/2 pairs");
            contests.Should().OnlyContain(c => c.HomeParticipantId != c.AwayParticipantId);
            contests.Select(UnorderedPair).Should().OnlyHaveUniqueItems();
            contests.SelectMany(c => new[] { c.HomeParticipantId, c.AwayParticipantId })
                .Distinct().Should().HaveCount(count);
            contests.Select(c => c.Round).Distinct().Should().HaveCount(engine.CalculateRounds(count, false));
        }
    }

    [Fact]
    public void DoubleRoundRobin_HasOneFixtureInEachDirectionForEveryPair()
    {
        var engine = new RoundRobinEngine();

        for (var count = 2; count <= 20; count++)
        {
            var contests = engine.GenerateContests(
                Stage(StageType.League), Participants(count),
                new RoundRobinOptions { SportId = SportId, HomeAndAway = true }).ToList();

            contests.Should().HaveCount(count * (count - 1));
            foreach (var pair in contests.GroupBy(UnorderedPair))
            {
                pair.Should().HaveCount(2);
                pair.Select(c => $"{c.HomeParticipantId}:{c.AwayParticipantId}")
                    .Should().OnlyHaveUniqueItems();
            }
        }
    }

    [Fact]
    public void RoundRobin_DrawIsReproducibleFromStoredSeed()
    {
        var engine = new RoundRobinEngine();
        var options = new RoundRobinOptions
        {
            SportId = SportId,
            HomeAndAway = true,
            DrawSeed = 20260811
        };

        var first = engine.GenerateContests(Stage(StageType.League), Participants(9), options)
            .Select(item => (item.Round, item.HomeParticipantId, item.AwayParticipantId));
        var second = engine.GenerateContests(Stage(StageType.League), Participants(9), options)
            .Select(item => (item.Round, item.HomeParticipantId, item.AwayParticipantId));

        first.Should().Equal(second);
    }

    [Fact]
    public void Knockout_AlwaysBuildsACompletePowerOfTwoBracket()
    {
        var engine = new KnockoutEngine();

        for (var count = 2; count <= 64; count++)
        {
            var contests = engine.GenerateContests(
                Stage(StageType.Knockout), Participants(count),
                new KnockoutOptions { SportId = SportId, RandomDraw = false }).ToList();
            var bracketSize = NextPowerOfTwo(count);

            contests.Should().HaveCount(bracketSize - 1);
            contests.Count(c => c.Round == 1).Should().Be(bracketSize / 2);
            engine.CalculateByes(count).Should().Be(bracketSize - count);
            engine.CalculateRounds(count, false).Should().Be((int)Math.Log2(bracketSize));
            contests.Where(c => c.Round == 1)
                .SelectMany(c => new[] { c.HomeParticipantId, c.AwayParticipantId })
                .Where(id => id.HasValue).Should().OnlyHaveUniqueItems().And.HaveCount(count);
        }
    }

    [Fact]
    public void Knockout_TwoLegTiesReverseHomeSidesAndAddOneThirdPlacePlayoff()
    {
        var contests = new KnockoutEngine().GenerateContests(
            Stage(StageType.Knockout),
            Participants(4),
            new KnockoutOptions
            {
                SportId = SportId,
                RandomDraw = false,
                LegsPerTie = 2,
                IncludeThirdPlace = true
            }).ToList();

        contests.Should().HaveCount(7);
        contests.Where(item => !item.IsThirdPlace)
            .GroupBy(item => item.TieId)
            .Should().HaveCount(3).And.OnlyContain(group => group.Count() == 2);
        foreach (var tie in contests.Where(item => !item.IsThirdPlace).GroupBy(item => item.TieId))
        {
            var legs = tie.OrderBy(item => item.LegNumber).ToArray();
            legs[0].HomeParticipantId.Should().Be(legs[1].AwayParticipantId);
            legs[0].AwayParticipantId.Should().Be(legs[1].HomeParticipantId);
            legs.Should().OnlyContain(item => item.Contest.SeriesBestOf == 2);
        }
        contests.Single(item => item.IsThirdPlace).TieId.Should().BeNull();
    }

    [Fact]
    public void Knockout_RandomDrawIsReproducibleFromItsSeed()
    {
        var engine = new KnockoutEngine();
        var options = new KnockoutOptions
        {
            SportId = SportId,
            RandomDraw = true,
            DrawSeed = 8675309
        };

        var first = engine.GenerateContests(Stage(StageType.Knockout), Participants(8), options)
            .Where(item => item.Round == 1)
            .Select(item => (item.HomeParticipantId, item.AwayParticipantId));
        var second = engine.GenerateContests(Stage(StageType.Knockout), Participants(8), options)
            .Where(item => item.Round == 1)
            .Select(item => (item.HomeParticipantId, item.AwayParticipantId));

        first.Should().Equal(second);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 4)]
    public void BestOfSeries_UsesPositiveOddGameCountsAndAlternatesHomeAdvantage(int games, int winsRequired)
    {
        var participants = Participants(2);
        var contests = new BestOfSeriesEngine().GenerateContests(
            Stage(StageType.Knockout), participants,
            new BestOfSeriesOptions { SportId = SportId, TotalGames = games, AlternateHomeGames = true }).ToList();

        contests.Should().HaveCount(games);
        new BestOfSeriesEngine().GamesToWin(games).Should().Be(winsRequired);
        contests.Select((contest, index) => contest.HomeParticipantId == participants[index % 2].ParticipantId)
            .Should().OnlyContain(matches => matches);
    }

    [Fact]
    public void BestOfSeries_RejectsEvenGameCountsAndAnythingOtherThanTwoParticipants()
    {
        var engine = new BestOfSeriesEngine();
        var action = () => engine.GenerateContests(Stage(StageType.Knockout), Participants(2),
            new BestOfSeriesOptions { SportId = SportId, TotalGames = 4 }).ToList();
        var wrongParticipants = () => engine.GenerateContests(Stage(StageType.Knockout), Participants(3),
            new BestOfSeriesOptions { SportId = SportId, TotalGames = 3 }).ToList();

        action.Should().Throw<ArgumentOutOfRangeException>();
        wrongParticipants.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(3, "1,2,1")]
    [InlineData(5, "1,1,2,2,1")]
    [InlineData(7, "1,1,2,2,1,2,1")]
    public void BestOfSeries_PersistsConfiguredHomeSequence(int games, string configured)
    {
        var participants = Participants(2);
        var sequence = configured.Split(',').Select(int.Parse).ToArray();
        var contests = new BestOfSeriesEngine().GenerateContests(
            Stage(StageType.Knockout),
            participants,
            new BestOfSeriesOptions
            {
                SportId = SportId,
                TotalGames = games,
                HomeSequence = sequence,
                HomeAdvantage = HomeAdvantage.HigherSeed
            }).ToArray();

        contests.Select(item => item.Contest.SeriesHomeParticipantSlot)
            .Should().Equal(sequence.Select(slot => (int?)slot));
        contests.Select(item => item.HomeParticipantId).Should().Equal(
            sequence.Select(slot => (Guid?)participants[slot - 1].ParticipantId));
    }

    [Fact]
    public void HeatAssignments_AreCompleteUniqueAndWithinConfiguredCapacity()
    {
        var engine = new HeatQualificationEngine();

        for (var count = 2; count <= 48; count++)
        {
            var assignments = engine.AssignToHeats(Participants(count), new HeatQualificationOptions
            {
                ParticipantsPerHeat = 8,
                LanesPerHeat = 8,
                UseSeeding = true
            }).ToList();

            assignments.Should().HaveCount(count);
            assignments.Select(a => a.ParticipantId).Should().OnlyHaveUniqueItems();
            assignments.Should().OnlyContain(a => a.HeatNumber >= 1 && a.Lane >= 1 && a.Lane <= 8);
            assignments.GroupBy(a => a.HeatNumber).Should().OnlyContain(group => group.Count() <= 8);
        }
    }

    [Fact]
    public void HeatAssignments_UseSerpentineSeedsAndCentreOutLanes()
    {
        var participants = Participants(16, "Athlete");
        var assignments = new HeatQualificationEngine().AssignToHeats(
            participants,
            new HeatQualificationOptions
            {
                ParticipantsPerHeat = 8,
                LanesPerHeat = 8,
                UseSeeding = true,
                UseSerpentineSeeding = true,
                LaneAssignment = "Seeded"
            }).ToArray();

        assignments.Single(item => item.ParticipantId == participants[0].ParticipantId)
            .Should().Match<HeatAssignment>(item => item.HeatNumber == 1 && item.Lane == 4);
        assignments.Single(item => item.ParticipantId == participants[1].ParticipantId)
            .Should().Match<HeatAssignment>(item => item.HeatNumber == 2 && item.Lane == 4);
        assignments.Single(item => item.ParticipantId == participants[2].ParticipantId)
            .HeatNumber.Should().Be(2);
        assignments.Single(item => item.ParticipantId == participants[3].ParticipantId)
            .HeatNumber.Should().Be(1);
        assignments.GroupBy(item => item.HeatNumber).Should()
            .OnlyContain(group => group.Select(item => item.Lane).Distinct().Count()
                == group.Count());
    }

    [Fact]
    public void GroupPlusKnockout_GeneratesAllGroupPairsWithoutCrossGroupFixtures()
    {
        var stage = Stage(StageType.GroupAndKnockout);
        var results = new GroupPlusKnockoutEngine().GenerateContests(
            stage, Participants(12),
            new GroupPlusKnockoutOptions
            {
                SportId = SportId,
                GroupsCount = 3,
                TeamsPerGroup = 4,
                QualifiersPerGroup = 2
            }).ToList();

        results.Should().HaveCount(18);
        results.GroupBy(r => r.GroupName).Should().HaveCount(3)
            .And.OnlyContain(group => group.Count() == 6);
        results.Select(UnorderedPair).Should().OnlyHaveUniqueItems();
        stage.Groups.Should().HaveCount(3);
        results.Should().OnlyContain(result => result.Contest.StageId == stage.Id
            && result.Contest.GroupId.HasValue);
    }

    [Fact]
    public void MultiRoundLeaderboard_GeneratesConfiguredRoundsAndRanksLowerScoresFirst()
    {
        var participants = Participants(8);
        var options = new MultiRoundLeaderboardOptions
        {
            SportId = SportId,
            TotalRounds = 4,
            GroupContestSize = 4,
            RandomizeGroups = false,
            LowerIsBetter = true,
            CutAfterRound = 2,
            CutToPlayers = 4
        };
        var engine = new MultiRoundLeaderboardEngine();

        var contests = engine.GenerateContests(Stage(StageType.Leaderboard), participants, options).ToList();
        var scores = participants.SelectMany((participant, index) => Enumerable.Range(1, 2)
            .Select(round => new RoundScore(participant.ParticipantId, round, 70 + index))).ToList();
        var leaderboard = engine.CalculateLeaderboard(scores, options);

        contests.Should().HaveCount(8);
        contests.GroupBy(c => c.Round).Should().HaveCount(4).And.OnlyContain(group => group.Count() == 2);
        leaderboard.Leader!.ParticipantId.Should().Be(participants[0].ParticipantId);
        engine.ApplyCut(participants, scores, options).Should().HaveCount(4);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MultiRoundLeaderboard_RanksEveryRoundAndRecalculatesBothDirections(
        bool lowerIsBetter)
    {
        var participants = Participants(4, "Athlete");
        var first = participants[0].ParticipantId;
        var second = participants[1].ParticipantId;
        var withdrawn = participants[2].ParticipantId;
        var disqualified = participants[3].ParticipantId;
        var direction = lowerIsBetter ? 1 : -1;
        var scores = new List<RoundScore>
        {
            new(first, 1, 10),
            new(first, 2, 10),
            new(second, 1, 10 + direction),
            new(second, 2, 10),
            new(withdrawn, 1, 10 + (2 * direction),
                LeaderboardParticipantStatus.Withdrawn),
            new(disqualified, 1, 10 - direction,
                LeaderboardParticipantStatus.Disqualified)
        };
        var options = new MultiRoundLeaderboardOptions
        {
            TotalRounds = 2,
            LowerIsBetter = lowerIsBetter,
            CutAfterRound = null,
            CutToPlayers = null
        };
        var engine = new MultiRoundLeaderboardEngine();

        var initial = engine.CalculateLeaderboard(scores, options);
        initial.Leader!.ParticipantId.Should().Be(first);
        initial.Entries.Single(entry => entry.ParticipantId == first)
            .RoundPositions.Should().Equal(
                new Dictionary<int, int> { [1] = 1, [2] = 1 });
        initial.Entries.Single(entry => entry.ParticipantId == withdrawn)
            .Status.Should().Be("Withdrawn");
        initial.Entries.Single(entry => entry.ParticipantId == disqualified)
            .Status.Should().Be("Disqualified");

        var corrected = scores
            .Select(score => score.ParticipantId == second && score.Round == 1
                ? score with { Score = 10 - (5 * direction) }
                : score)
            .ToArray();
        var recalculated = engine.CalculateLeaderboard(corrected, options);

        recalculated.Leader!.ParticipantId.Should().Be(second);
        recalculated.Entries.Single(entry => entry.ParticipantId == second)
            .RoundPositions.Values.Should().OnlyContain(position => position == 1);
        recalculated.Entries.Take(2).Select(entry => entry.ParticipantId)
            .Should().Equal(second, first);
    }

    [Fact]
    public void FightCard_PairsEveryFighterExactlyOnce()
    {
        var participants = Participants(10, "Fighter");
        var results = new FightCardEngine().GenerateContests(
            Stage(StageType.FightCard), participants,
            new FightCardOptions { SportId = SportId, DefaultWeightClass = "Middleweight" }).ToList();

        results.Should().HaveCount(5);
        results.SelectMany(r => new[] { r.Fighter1?.ParticipantId, r.Fighter2?.ParticipantId })
            .Where(id => id.HasValue).Should().OnlyHaveUniqueItems().And.HaveCount(10);
    }

    [Fact]
    public void FightCard_ReorderRequiresCompleteOrderAndPreservesSectionHierarchy()
    {
        var card = new FightCard
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = "Test card",
            Bouts = new List<FightCardBout>
            {
                Bout(1, "PreliminaryCard"),
                Bout(2, "PreliminaryCard"),
                Bout(3, "MainCard"),
                Bout(4, "CoMainEvent"),
                Bout(5, "MainEvent")
            }
        };
        var engine = new FightCardEngine();

        engine.ReorderFightCard(card, new[]
        {
            GuidFromIndex(2), GuidFromIndex(1), GuidFromIndex(3),
            GuidFromIndex(4), GuidFromIndex(5)
        });

        card.Bouts.Select(bout => bout.Id).Should().Equal(
            GuidFromIndex(2), GuidFromIndex(1), GuidFromIndex(3),
            GuidFromIndex(4), GuidFromIndex(5));
        card.Bouts.Select(bout => bout.BoutOrder).Should().Equal(1, 2, 3, 4, 5);
        engine.ValidateFightCard(card).IsValid.Should().BeTrue();

        var invalid = () => engine.ReorderFightCard(card, new[]
        {
            GuidFromIndex(5), GuidFromIndex(2), GuidFromIndex(1),
            GuidFromIndex(3), GuidFromIndex(4)
        });
        invalid.Should().Throw<InvalidOperationException>()
            .WithMessage("*sections*");
    }

    private static FightCardBout Bout(int index, string section) => new()
    {
        Id = GuidFromIndex(index),
        Fighter1Id = GuidFromIndex(100 + (index * 2)),
        Fighter2Id = GuidFromIndex(101 + (index * 2)),
        WeightClass = "Middleweight",
        BoutType = section,
        BoutOrder = index,
        ScheduledRounds = section == "MainEvent" ? 5 : 3
    };

    private static Stage Stage(StageType type) =>
        new(Guid.NewGuid(), Guid.NewGuid(), TenantId, "Test stage", type, 1);

    private static List<ParticipantInfo> Participants(int count, string type = "Team") =>
        Enumerable.Range(1, count)
            .Select(index => new ParticipantInfo(GuidFromIndex(index), type, index))
            .ToList();

    private static string UnorderedPair(ContestGenerationResult result) =>
        string.Join(':', new[] { result.HomeParticipantId!.Value, result.AwayParticipantId!.Value }.Order());

    private static Guid GuidFromIndex(int index) =>
        Guid.Parse($"30000000-0000-0000-0000-{index:D12}");

    private static int NextPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value) result *= 2;
        return result;
    }
}
