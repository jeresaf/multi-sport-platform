using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface IBasketballRulesetService
{
    Task<SportDefinitionVersion> ConfigureBasketballRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetBasketballRulesetAsync(CancellationToken cancellationToken = default);
}

public class BasketballRulesetService : IBasketballRulesetService
{
    private readonly ISportsService _sportsService;
    private const string BasketballCode = "BASKETBALL";

    public BasketballRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureBasketballRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // Get or create basketball sport
        var basketball = await _sportsService.GetSportByCodeAsync(BasketballCode, cancellationToken);
        if (basketball == null)
        {
            basketball = await _sportsService.CreateSportAsync(
                BasketballCode,
                "Basketball",
                "Basketball - 5v5 team sport with four quarters",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            basketball.Id,
            "Standard Rules v1.0",
            createdBy,
            cancellationToken);

        // Configure the version with basketball-specific rules
        version.ConfigureContestShape(
            contestShape: "TeamVsTeam",
            participantType: "Team",
            segmentDefinitions: GetSegmentDefinitions(),
            eventDefinitions: GetEventDefinitions(),
            metricDefinitions: GetMetricDefinitions(),
            resultMethods: GetResultMethods(),
            rankingRules: GetRankingRules(),
            displayConfiguration: GetDisplayConfiguration(),
            validationConfiguration: GetValidationConfiguration()
        );

        await _sportsService.UpdateSportAsync(basketball, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetBasketballRulesetAsync(CancellationToken cancellationToken = default)
    {
        var basketball = await _sportsService.GetSportByCodeAsync(BasketballCode, cancellationToken);
        if (basketball == null) return null;

        // Get the latest published version
        return basketball.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        var segments = new object[]
        {
            new { id = "Q1", name = "First Quarter", type = "Quarter", order = 1, durationMinutes = 12, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "Q2" },
            new { id = "Q2", name = "Second Quarter", type = "Quarter", order = 2, durationMinutes = 12, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "HALF_TIME" },
            new { id = "HALF_TIME", name = "Half Time", type = "Break", order = 3, durationMinutes = 15, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "Q3" },
            new { id = "Q3", name = "Third Quarter", type = "Quarter", order = 4, durationMinutes = 12, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "Q4" },
            new { id = "Q4", name = "Fourth Quarter", type = "Quarter", order = 5, durationMinutes = 12, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "OVERTIME_1" },
            new { id = "OVERTIME_1", name = "Overtime 1", type = "Overtime", order = 6, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "OVERTIME_BREAK" },
            new { id = "OVERTIME_BREAK", name = "Overtime Break", type = "Break", order = 7, durationMinutes = 1, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "OVERTIME_2" },
            new { id = "OVERTIME_2", name = "Overtime 2", type = "Overtime", order = 8, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "OVERTIME_BREAK_2" },
            new { id = "OVERTIME_BREAK_2", name = "Overtime Break 2", type = "Break", order = 9, durationMinutes = 1, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "OVERTIME_3" },
            new { id = "OVERTIME_3", name = "Overtime 3", type = "Overtime", order = 10, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = (string?)null }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
        => BasketballTimelinePackage.GetDefinitionsJson();

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "POINTS",
                name = "Points",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "FIELD_GOALS_MADE",
                name = "Field Goals Made",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FIELD_GOALS_ATTEMPTED",
                name = "Field Goals Attempted",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FIELD_GOAL_PERCENTAGE",
                name = "Field Goal %",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "THREE_POINTERS_MADE",
                name = "Three Pointers Made",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "THREE_POINTERS_ATTEMPTED",
                name = "Three Pointers Attempted",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "THREE_POINT_PERCENTAGE",
                name = "Three Point %",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FREE_THROWS_MADE",
                name = "Free Throws Made",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FREE_THROWS_ATTEMPTED",
                name = "Free Throws Attempted",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FREE_THROW_PERCENTAGE",
                name = "Free Throw %",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "REBOUNDS",
                name = "Rebounds",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "OFFENSIVE_REBOUNDS",
                name = "Offensive Rebounds",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "DEFENSIVE_REBOUNDS",
                name = "Defensive Rebounds",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "ASSISTS",
                name = "Assists",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TURNOVERS",
                name = "Turnovers",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "STEALS",
                name = "Steals",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BLOCKS",
                name = "Blocks",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PERSONAL_FOULS",
                name = "Personal Fouls",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TEAM_FOULS",
                name = "Team Fouls",
                type = "Integer",
                aggregation = "SumBySegment",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TIMEOUTS",
                name = "Timeouts Used",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            }
        };

        return JsonSerializer.Serialize(metrics);
    }

    public static string GetResultMethods()
    {
        var results = new[]
        {
            new
            {
                id = "WIN",
                name = "Win",
                type = "WinLoss",
                pointsAwarded = 1,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "LOSS",
                name = "Loss",
                type = "WinLoss",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "WIN_OVERTIME",
                name = "Win in Overtime",
                type = "WinLoss",
                pointsAwarded = 1,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "LOSS_OVERTIME",
                name = "Loss in Overtime",
                type = "WinLoss",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = true
            }
        };

        return JsonSerializer.Serialize(results);
    }

    public static string GetRankingRules()
    {
        var rules = new[]
        {
            new
            {
                order = 1,
                criteria = "WinPercentage",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 2,
                criteria = "HeadToHead",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 3,
                criteria = "DivisionWinningPercentage",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 4,
                criteria = "PointsScored",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 5,
                criteria = "PointsAllowed",
                direction = "Ascending",
                tiebreaker = true
            }
        };

        return JsonSerializer.Serialize(rules);
    }

    public static string GetDisplayConfiguration()
    {
        var config = new
        {
            primaryScoreboard = new
            {
                showPoints = true,
                showQuarter = true,
                showTimeRemaining = true,
                showTimeouts = true,
                showFouls = true,
                showPlayerStats = true
            },
            timeline = new
            {
                showScoring = true,
                showFouls = true,
                showSubstitutions = true,
                showTimeouts = true
            },
            standings = new
            {
                showPlayed = true,
                showWon = true,
                showLost = true,
                showWinPercentage = true,
                showHomeRecord = true,
                showAwayRecord = true
            },
            colors = new
            {
                homeTeam = "#1565C0",
                awayTeam = "#C62828",
                win = "#2E7D32",
                loss = "#C62828",
                overtime = "#FF8F00"
            }
        };

        return JsonSerializer.Serialize(config);
    }

    public static string GetValidationConfiguration()
    {
        var config = new
        {
            contest = new
            {
                minParticipants = 2,
                maxParticipants = 2,
                allowTies = false,
                maxOvertimePeriods = 3,
                requiresOvertimeIfTied = true
            },
            participants = new
            {
                minRosterSize = 12,
                maxRosterSize = 20,
                minOnField = 5,
                maxOnField = 5,
                maxSubstitutions = 99,
                allowUnlimitedSubstitutions = true
            },
            events = new
            {
                allowNegativeScore = false,
                allowScoreAfterFinalWhistle = false,
                validateMinuteSequence = true,
                maxEventsPerMinute = 10
            },
            scoring = new
            {
                allowedPointValues = new[] { 1, 2, 3 },
                requireCanonicalEventType = true,
                aggregatePlayerPointsIntoTeamTotal = true
            },
            fouls = new
            {
                teamFoulsResetEachPeriod = true,
                bonusThresholdPerPeriod = 5,
                personalFoulDisqualificationThreshold = 6
            },
            timeouts = new
            {
                regulationPerTeam = 7,
                overtimePerTeam = 2
            },
            series = new
            {
                supported = true,
                allowedBestOf = new[] { 1, 3, 5, 7 },
                requireWinner = true
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = false,
                allowClockStoppage = true,
                allowEarlyStart = false,
                shotClockRequired = true,
                shotClockDuration = 24
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
