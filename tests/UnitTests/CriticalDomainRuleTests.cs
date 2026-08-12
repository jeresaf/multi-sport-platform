using FluentAssertions;
using Moq;
using System.Text.Json;
using SportsPlatform.Modules.Contests;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Services;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Standings;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Subscriptions.Entities;
using Xunit;
using SportsPlatform.Modules.Brackets.Entities;

namespace SportsPlatform.UnitTests;

public sealed class CriticalDomainRuleTests
{
    [Fact]
    public void ScoringSchemaCatalog_DefinesInteractiveControlsForEveryPrototypeSport()
    {
        var prototypeSports = new Dictionary<Guid, string>
        {
            [Guid.Parse("00000000-0000-0000-0000-000000000100")] = "football",
            [Guid.Parse("00000000-0000-0000-0000-000000000101")] = "basketball",
            [Guid.Parse("00000000-0000-0000-0000-000000000102")] = "golf",
            [Guid.Parse("00000000-0000-0000-0000-000000000103")] = "athletics",
            [Guid.Parse("00000000-0000-0000-0000-000000000104")] = "cricket",
            [Guid.Parse("00000000-0000-0000-0000-000000000105")] = "combat",
            [Guid.Parse("00000000-0000-0000-0000-000000000106")] = "rugby"
        };
        var segments = new[]
        {
            new ScoringSegmentSchema(
                Guid.NewGuid(), "Opening segment", "Period", 1, "Scheduled")
        };

        foreach (var (sportId, expectedCode) in prototypeSports)
        {
            var schema = ScoringSchemaCatalog.ForSport(sportId, segments);

            schema.Should().NotBeNull();
            schema!.SportCode.Should().Be(expectedCode);
            schema.Actions.Should().NotBeEmpty();
            schema.Actions.Should().OnlyContain(action =>
                !string.IsNullOrWhiteSpace(action.Code)
                && !string.IsNullOrWhiteSpace(action.Label)
                && !string.IsNullOrWhiteSpace(action.Metric)
                && (action.InputType == "fixed" || action.InputType == "number"));
            schema.Actions.Where(action => action.InputType == "number")
                .All(action =>
                    action.Minimum.HasValue
                    && action.Maximum.HasValue
                    && action.Step.HasValue
                    && action.Minimum <= action.DefaultValue
                    && action.DefaultValue <= action.Maximum)
                .Should().BeTrue();
            schema.Actions.Should().OnlyContain(action => action.RequiresSegment);
        }
    }

    [Fact]
    public void FootballRanking_UsesPointsThenDifferentialThenGoalsForAndSharesTrueTies()
    {
        var first = Id(1);
        var second = Id(2);
        var tied = Id(3);
        var ranked = StandingRankingRules.Rank(new[]
        {
            new StandingRankCandidate(second, 9, 8, 4),
            new StandingRankCandidate(first, 9, 10, 5),
            new StandingRankCandidate(tied, 9, 10, 5),
            new StandingRankCandidate(Id(4), 8, 30, 1)
        }, StandingRankingProfile.Football);

        ranked.Select(item => item.ParticipantId)
            .Should().Equal(first, tied, second, Id(4));
        ranked.Select(item => item.Position).Should().Equal(1, 1, 3, 4);
    }

    [Fact]
    public void FootballRanking_UsesHeadToHeadAfterPointsDifferenceAndGoalsScored()
    {
        var first = Id(10);
        var second = Id(11);
        var ranked = StandingRankingRules.Rank(
            new[]
            {
                new StandingRankCandidate(first, 12, 8, 4),
                new StandingRankCandidate(second, 12, 8, 4)
            },
            StandingRankingProfile.Football,
            new Dictionary<Guid, StandingHeadToHead>
            {
                [first] = new(3, 2, 1),
                [second] = new(0, 1, 2)
            });

        ranked.Select(item => item.ParticipantId).Should().Equal(first, second);
        ranked.Select(item => item.Position).Should().Equal(1, 2);
    }

    [Fact]
    public void FootballExtension_DefinesEveryRequiredVersionedRule()
    {
        using var segments = JsonDocument.Parse(FootballRulesetService.GetSegmentDefinitions());
        using var events = JsonDocument.Parse(FootballRulesetService.GetEventDefinitions());
        using var methods = JsonDocument.Parse(FootballRulesetService.GetResultMethods());
        using var ranking = JsonDocument.Parse(FootballRulesetService.GetRankingRules());

        segments.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[]
            {
                "FIRST_HALF", "SECOND_HALF", "EXTRA_TIME_FIRST_HALF",
                "EXTRA_TIME_SECOND_HALF", "PENALTY_SHOOTOUT"
            });
        events.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[]
            {
                "GOAL", "OWN_GOAL", "PENALTY_GOAL", "YELLOW_CARD", "RED_CARD",
                "SUBSTITUTION_IN", "PENALTY_SCORED", "PENALTY_MISSED"
            });
        methods.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "WIN", "DRAW", "LOSS", "WIN_PENALTIES" });
        ranking.RootElement.EnumerateArray().Select(item => item.GetProperty("criteria").GetString())
            .Should().ContainInOrder("Points", "GoalDifference", "GoalsFor", "HeadToHead");

        var sport = new Sport(Id(12), "FOOTBALL", "Football");
        sport.CreateNewVersion("Standard v1", "test").VersionNumber.Should().Be(1);
        sport.CreateNewVersion("Standard v2", "test").VersionNumber.Should().Be(2);
    }

    [Fact]
    public void BasketballExtension_DefinesPointsFoulsTimeoutsOvertimeAndSeriesRules()
    {
        using var segments = JsonDocument.Parse(BasketballRulesetService.GetSegmentDefinitions());
        using var events = JsonDocument.Parse(BasketballRulesetService.GetEventDefinitions());
        using var metrics = JsonDocument.Parse(BasketballRulesetService.GetMetricDefinitions());
        using var validation = JsonDocument.Parse(BasketballRulesetService.GetValidationConfiguration());

        segments.RootElement.EnumerateArray()
            .Count(item => item.GetProperty("type").GetString() == "Quarter")
            .Should().Be(4);
        segments.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "Overtime")
            .Should().OnlyContain(item => item.GetProperty("isOptional").GetBoolean());

        var eventMap = events.RootElement.EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                item => item);
        eventMap["FREE_THROW_MADE"].GetProperty("points").GetInt32().Should().Be(1);
        eventMap["FIELD_GOAL_MADE"].GetProperty("points").GetInt32().Should().Be(2);
        eventMap["THREE_POINTER_MADE"].GetProperty("points").GetInt32().Should().Be(3);
        eventMap["TEAM_FOUL"].GetProperty("resetsEachPeriod").GetBoolean().Should().BeTrue();
        eventMap["PERSONAL_FOUL"].GetProperty("requiresPlayer").GetBoolean().Should().BeTrue();
        eventMap.Should().ContainKey("TIMEOUT");

        metrics.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "POINTS", "TEAM_FOULS", "PERSONAL_FOULS", "TIMEOUTS" });
        validation.RootElement.GetProperty("scoring")
            .GetProperty("allowedPointValues").EnumerateArray()
            .Select(item => item.GetInt32()).Should().Equal(1, 2, 3);
        validation.RootElement.GetProperty("contest")
            .GetProperty("allowTies").GetBoolean().Should().BeFalse();
        validation.RootElement.GetProperty("series")
            .GetProperty("allowedBestOf").EnumerateArray()
            .Select(item => item.GetInt32()).Should().Equal(1, 3, 5, 7);

        var schema = ScoringSchemaCatalog.ForSport(
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            Array.Empty<ScoringSegmentSchema>());
        schema.Should().NotBeNull();
        schema!.Actions.Where(item => item.Metric == "score")
            .Select(item => (item.EventType, item.DefaultValue))
            .Should().Equal(
                ("FREE_THROW_MADE", 1m),
                ("FIELD_GOAL_MADE", 2m),
                ("THREE_POINTER_MADE", 3m));
        schema.Actions.Single(item => item.Code == "personal-foul")
            .RequiresPlayer.Should().BeTrue();

        var sport = new Sport(Id(13), "BASKETBALL", "Basketball");
        sport.CreateNewVersion("Standard v1", "test").VersionNumber.Should().Be(1);
        sport.CreateNewVersion("Standard v2", "test").VersionNumber.Should().Be(2);
    }

    [Fact]
    public void AthleticsRuleset_DefinesTrackFieldQualificationRecordsAndMedals()
    {
        using var segments = JsonDocument.Parse(
            AthleticsRulesetService.GetSegmentDefinitions());
        using var events = JsonDocument.Parse(
            AthleticsRulesetService.GetEventDefinitions());
        using var metrics = JsonDocument.Parse(
            AthleticsRulesetService.GetMetricDefinitions());
        using var ranking = JsonDocument.Parse(
            AthleticsRulesetService.GetRankingRules());
        using var display = JsonDocument.Parse(
            AthleticsRulesetService.GetDisplayConfiguration());
        using var validation = JsonDocument.Parse(
            AthleticsRulesetService.GetValidationConfiguration());

        segments.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .Should().Contain(new[] { "Heat", "Final", "Round", "Group" });
        events.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[]
            {
                "FINISH", "DQ", "JUMP_VALID", "CLEARANCE", "THROW_VALID"
            });
        metrics.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "FINISH_TIME", "DISTANCE", "HEIGHT" });
        ranking.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("direction").GetString())
            .Should().Contain(new[] { "Ascending", "Descending" });
        display.RootElement.GetProperty("primaryScoreboard")
            .GetProperty("showRecordMark").GetBoolean().Should().BeTrue();
        validation.RootElement.GetProperty("contest")
            .GetProperty("qualifyByPlace").GetInt32().Should().BeGreaterThan(0);

        var fieldEvent = new Contest(Id(14), Id(15), Id(16), "AthleticsFieldEvent");
        fieldEvent.ConfigureAthleticsFieldEvent("Long Jump", "Distance", 6, 8.95m);
        fieldEvent.AthleticsMetricType.Should().Be("Distance");
        fieldEvent.AthleticsMaxAttempts.Should().Be(6);
        fieldEvent.AthleticsRecordThreshold.Should().Be(8.95m);
    }

    [Fact]
    public void CricketT20Ruleset_DefinesCompositeScoreSequenceAndTieHandling()
    {
        using var segments = JsonDocument.Parse(
            CricketT20RulesetService.GetSegmentDefinitions());
        using var events = JsonDocument.Parse(
            CricketT20RulesetService.GetEventDefinitions());
        using var metrics = JsonDocument.Parse(
            CricketT20RulesetService.GetMetricDefinitions());
        using var methods = JsonDocument.Parse(
            CricketT20RulesetService.GetResultMethods());
        using var display = JsonDocument.Parse(
            CricketT20RulesetService.GetDisplayConfiguration());
        using var validation = JsonDocument.Parse(
            CricketT20RulesetService.GetValidationConfiguration());

        segments.RootElement.EnumerateArray()
            .Count(item => item.GetProperty("type").GetString() == "InningsPhase")
            .Should().Be(6);
        segments.RootElement.EnumerateArray().Single(item =>
                item.GetProperty("id").GetString() == "SUPER_OVER")
            .GetProperty("isOptional").GetBoolean().Should().BeTrue();
        events.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[]
            {
                "RUN_0", "RUN_4", "RUN_6", "BOWLED", "WIDE", "NO_BALL",
                "INNINGS_END", "MATCH_END"
            });
        metrics.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "RUNS", "WICKETS", "OVERS", "EXTRAS" });
        methods.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "WIN", "LOSS", "TIE", "NO_RESULT" });
        display.RootElement.GetProperty("primaryScoreboard")
            .GetProperty("showRequiredRate").GetBoolean().Should().BeTrue();

        var contestRules = validation.RootElement.GetProperty("contest");
        contestRules.GetProperty("oversPerInnings").GetInt32().Should().Be(20);
        contestRules.GetProperty("ballsPerOver").GetInt32().Should().Be(6);
        contestRules.GetProperty("maxParticipants").GetInt32().Should().Be(2);
        contestRules.GetProperty("allowTies").GetBoolean().Should().BeTrue();

        var sport = new Sport(Id(18), "CRICKET", "Cricket");
        sport.CreateNewVersion("T20 v1", "test").VersionNumber.Should().Be(1);
        sport.CreateNewVersion("T20 v2", "test").VersionNumber.Should().Be(2);
    }

    [Fact]
    public void CombatRuleset_DefinesRequiredRoundsMethodsWeightClassesAndJudging()
    {
        using var segments = JsonDocument.Parse(
            CombatSportsRulesetService.GetSegmentDefinitions());
        using var methods = JsonDocument.Parse(
            CombatSportsRulesetService.GetResultMethods());
        using var display = JsonDocument.Parse(
            CombatSportsRulesetService.GetDisplayConfiguration());
        using var validation = JsonDocument.Parse(
            CombatSportsRulesetService.GetValidationConfiguration());

        var mmaRounds = segments.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("id").GetString()!.StartsWith("M_RD"))
            .ToArray();
        mmaRounds.Should().HaveCount(5);
        mmaRounds.Should().OnlyContain(round =>
            round.GetProperty("durationMinutes").GetInt32() == 5);

        methods.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .Should().Contain(new[]
            {
                "Knockout", "TechnicalKnockout", "Submission", "Decision",
                "Disqualification", "Draw", "NoContest"
            });
        display.RootElement.GetProperty("weightClasses")
            .GetArrayLength().Should().BeGreaterThanOrEqualTo(9);
        validation.RootElement.GetProperty("contest")
            .GetProperty("minParticipants").GetInt32().Should().Be(2);
        validation.RootElement.GetProperty("timing")
            .GetProperty("manualStoppageTime").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void GolfExtension_DefinesCourseParsScoringRankingCutAndStatuses()
    {
        using var segments = JsonDocument.Parse(GolfRulesetService.GetSegmentDefinitions());
        using var events = JsonDocument.Parse(GolfRulesetService.GetEventDefinitions());
        using var metrics = JsonDocument.Parse(GolfRulesetService.GetMetricDefinitions());
        using var methods = JsonDocument.Parse(GolfRulesetService.GetResultMethods());
        using var ranking = JsonDocument.Parse(GolfRulesetService.GetRankingRules());
        using var validation = JsonDocument.Parse(GolfRulesetService.GetValidationConfiguration());

        var rounds = segments.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "Round")
            .ToArray();
        rounds.Should().HaveCount(4);
        rounds.Should().OnlyContain(round =>
            round.GetProperty("nestedSegments").GetArrayLength() == 18);
        rounds.Should().OnlyContain(round =>
            round.GetProperty("nestedSegments").EnumerateArray()
                .Sum(hole => hole.GetProperty("par").GetInt32()) == 72);

        events.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "STROKES", "PENALTY_STROKE", "WD", "DQ" });
        metrics.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[]
            {
                "PAR", "PENALTY_STROKES", "TOTAL_STROKES", "TO_PAR"
            });
        methods.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(new[] { "TIE", "CUT", "WD", "DQ" });
        ranking.RootElement.EnumerateArray().Should().OnlyContain(item =>
            item.GetProperty("recalculateAfter").GetString() == "EachHoleOrRound");

        var course = validation.RootElement.GetProperty("course");
        course.GetProperty("holesPerRound").GetInt32().Should().Be(18);
        course.GetProperty("standardHolePars").EnumerateArray()
            .Sum(item => item.GetInt32()).Should().Be(72);
        validation.RootElement.GetProperty("ranking")
            .GetProperty("direction").GetString().Should().Be("LowerIsBetter");
        validation.RootElement.GetProperty("cut")
            .GetProperty("configurable").GetBoolean().Should().BeTrue();

        var contest = new Contest(Id(14), Id(15), Id(16), "MultiRoundLeaderboard");
        contest.ConfigureLeaderboard(
            72, 2, 12, "Test Course", "Nairobi", GolfRulesetService.StandardHolePars);
        contest.CourseName.Should().Be("Test Course");
        JsonSerializer.Deserialize<int[]>(contest.HoleParsJson!).Should().HaveCount(18);

        var sport = new Sport(Id(17), "GOLF", "Golf");
        sport.CreateNewVersion("Standard v1", "test").VersionNumber.Should().Be(1);
        sport.CreateNewVersion("Standard v2", "test").VersionNumber.Should().Be(2);
    }

    [Fact]
    public void RugbyRanking_UsesPointsDifferentialAndTries()
    {
        var moreTries = Id(1);
        var fewerTries = Id(2);
        var ranked = StandingRankingRules.Rank(new[]
        {
            new StandingRankCandidate(fewerTries, 12, 40, 30, 5),
            new StandingRankCandidate(moreTries, 12, 35, 25, 7)
        }, StandingRankingProfile.Rugby);

        ranked.Select(item => item.ParticipantId).Should().Equal(moreTries, fewerTries);
        ranked.Select(item => item.Position).Should().Equal(1, 2);
    }

    [Fact]
    public void Contest_FollowsTheScheduledScoringApprovalPath()
    {
        var contest = Contest();

        contest.Invoking(item => item.Start())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*InProgress*");
        contest.Status.Should().Be(ContestStatus.Draft);
        contest.Schedule("Test venue", DateTime.UtcNow.AddHours(1));
        contest.Start();
        contest.Status.Should().Be(ContestStatus.InProgress);
        contest.Complete();
        contest.SubmitForApproval();
        contest.Approve();

        contest.Status.Should().Be(ContestStatus.Approved);
        contest.StartTime.Should().NotBeNull();
        contest.EndTime.Should().NotBeNull();
    }

    [Fact]
    public void Contest_LifecycleRejectsInvalidMovesAndSupportsOperationalStates()
    {
        var contest = Contest();
        contest.Schedule("Lifecycle venue", DateTime.UtcNow.AddHours(1));
        contest.OpenCheckIn();
        contest.MarkReady();
        contest.Start();
        contest.Pause();
        contest.Resume();
        contest.Suspend();
        contest.Resume();
        contest.Complete();
        contest.SubmitForApproval();
        contest.Approve();
        contest.MarkCorrected();
        contest.SubmitForApproval();

        contest.Status.Should().Be(ContestStatus.PendingApproval);
        contest.Invoking(item => item.Start())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Contest_LifecycleCanBeRestrictedBySportPolicy()
    {
        var contest = Contest();
        var policy = new ContestLifecyclePolicy(Guid.NewGuid(), contest.TenantId, contest.SportId);
        policy.Configure(new[]
        {
            new ContestLifecycleTransition(ContestStatus.Draft, ContestStatus.Cancelled)
        });

        contest.Invoking(item => item.TransitionTo(
                ContestStatus.Scheduled, policy.GetTransitions()))
            .Should().Throw<InvalidOperationException>();
        contest.TransitionTo(ContestStatus.Cancelled, policy.GetTransitions());
        contest.AllowedTransitions(policy.GetTransitions()).Should().BeEmpty();
    }

    [Fact]
    public void Lease_EnforcesLifecycleAndArchivedTerminalState()
    {
        var lease = new Subscription(
            Id(20), Id(21), "Professional",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));

        lease.Invoking(item => item.MarkExpiring()).Should().Throw<InvalidOperationException>();
        lease.Activate();
        lease.MarkExpiring();
        lease.SetGracePeriod(DateTime.UtcNow.AddDays(7));
        lease.SetReadOnly();
        lease.CanWrite().Should().BeFalse();
        lease.Renew(DateTime.UtcNow.AddMonths(2));
        lease.Status.Should().Be(SubscriptionStatus.Active);
        lease.CanWrite().Should().BeTrue();
        lease.Archive();
        lease.Invoking(item => item.Renew(DateTime.UtcNow.AddMonths(3)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Result_RejectionAllowsResubmissionButApprovalAndPublicationAreFinal()
    {
        var result = new ContestResult(Id(30), Id(31), Id(32), Id(33));
        result.Invoking(item => item.Approve(Id(34))).Should().Throw<InvalidOperationException>();

        result.Submit();
        result.Reject("Incorrect score");
        result.Submit();
        result.Approve(Id(34));
        result.Publish();

        result.Status.Should().Be(ContestResultStatus.Published);
        result.ApprovedBy.Should().Be(Id(34));
        result.Invoking(item => item.Submit()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task RejectedCorrection_RestoresOriginalAndRetainsRejectedAuditEvent()
    {
        var contest = Contest();
        var original = contest.AddEvent(
            "score", Id(40), null, "5", Id(41), "device", 1, "original");
        original.Approve();
        var repository = new Mock<IContestRepository>();
        repository.Setup(item => item.GetByIdAsync(contest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contest);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var workflow = new CorrectionWorkflowService(repository.Object);

        var submitted = await workflow.SubmitCorrectionAsync(
            contest.Id, contest.TenantId, original.Id, "Wrong value",
            "score", original.ParticipantId, "7", Id(42));
        submitted.ResultType.Should().Be(CorrectionResultType.Submitted);
        original.Status.Should().Be(EventStatus.Superseded);

        var rejected = await workflow.RejectCorrectionAsync(
            contest.Id, contest.TenantId, submitted.CorrectionEventId!.Value,
            "Correction was not supported", Id(43));

        rejected.ResultType.Should().Be(CorrectionResultType.Rejected);
        original.Status.Should().Be(EventStatus.Approved);
        original.SupersededByEventId.Should().BeNull();
        original.CorrectionReason.Should().BeNull();
        contest.Events.Single(item => item.Id == submitted.CorrectionEventId)
            .Status.Should().Be(EventStatus.Rejected);
    }

    [Fact]
    public void BracketOverride_RequiresIndependentApprovalAndRetainsDecisionHistory()
    {
        var requestedBy = Id(50);
        var reviewedBy = Id(51);
        var advancementOverride = new BracketAdvancementOverride(
            Id(52), Id(53), Id(54), "Aggregate tie resolved on penalties", requestedBy);

        advancementOverride.Invoking(item => item.Approve(requestedBy, null))
            .Should().Throw<InvalidOperationException>();
        advancementOverride.Approve(reviewedBy, "Checked against the signed match report");

        advancementOverride.Status.Should().Be(BracketOverrideStatus.Approved);
        advancementOverride.ReviewedBy.Should().Be(reviewedBy);
        advancementOverride.ReviewedAt.Should().NotBeNull();
        advancementOverride.Reason.Should().Be("Aggregate tie resolved on penalties");
        advancementOverride.Invoking(item => item.Reject(reviewedBy, null))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BracketMatch_CanAdvanceWinnerAndLoserToFinalPlacements()
    {
        var match = new BracketMatch(Id(60), Id(61), 1);
        match.SetParticipants(Id(62), Id(63));
        match.AdvanceTo(Id(64), 1);
        match.AdvanceLoserTo(Id(65), 2);

        match.NextMatchId.Should().Be(Id(64));
        match.NextMatchSlot.Should().Be(1);
        match.LoserNextMatchId.Should().Be(Id(65));
        match.LoserNextMatchSlot.Should().Be(2);
    }

    private static Contest Contest() => new(Id(100), Id(101), Id(102), "TeamVsTeam");

    private static Guid Id(int value) =>
        Guid.Parse($"40000000-0000-0000-0000-{value:D12}");
}
