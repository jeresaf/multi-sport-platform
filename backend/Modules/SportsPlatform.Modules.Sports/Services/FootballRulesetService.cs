using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface IFootballRulesetService
{
    Task<SportDefinitionVersion> ConfigureFootballRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetFootballRulesetAsync(CancellationToken cancellationToken = default);
}

public class FootballRulesetService : IFootballRulesetService
{
    private readonly ISportsService _sportsService;
    private const string FootballCode = "FOOTBALL";

    public FootballRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureFootballRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // Get or create football sport
        var football = await _sportsService.GetSportByCodeAsync(FootballCode, cancellationToken);
        if (football == null)
        {
            football = await _sportsService.CreateSportAsync(
                FootballCode,
                "Football",
                "Association Football - 11v11 team sport with two halves",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            football.Id,
            "Standard Rules v1.0",
            createdBy,
            cancellationToken);

        // Configure the version with football-specific rules
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

        await _sportsService.UpdateSportAsync(football, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetFootballRulesetAsync(CancellationToken cancellationToken = default)
    {
        var football = await _sportsService.GetSportByCodeAsync(FootballCode, cancellationToken);
        if (football == null) return null;

        // Get the latest published version
        return football.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        var segments = new object[]
        {
            new { id = "FIRST_HALF", name = "First Half", type = "Half", order = 1, durationMinutes = 45, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "HALF_TIME" },
            new { id = "HALF_TIME", name = "Half Time", type = "Break", order = 2, durationMinutes = 15, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SECOND_HALF" },
            new { id = "SECOND_HALF", name = "Second Half", type = "Half", order = 3, durationMinutes = 45, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "EXTRA_TIME_FIRST_HALF" },
            new { id = "EXTRA_TIME_FIRST_HALF", name = "Extra Time - First Half", type = "Half", order = 4, durationMinutes = 15, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, activationCondition = "tie_after_regulation", nextSegmentId = "EXTRA_TIME_BREAK" },
            new { id = "EXTRA_TIME_BREAK", name = "Extra Time Break", type = "Break", order = 5, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "EXTRA_TIME_SECOND_HALF" },
            new { id = "EXTRA_TIME_SECOND_HALF", name = "Extra Time - Second Half", type = "Half", order = 6, durationMinutes = 15, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, nextSegmentId = "PENALTY_SHOOTOUT" },
            new { id = "PENALTY_SHOOTOUT", name = "Penalty Shootout", type = "Shootout", order = 7, durationMinutes = 0, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, activationCondition = "tie_after_extra_time", nextSegmentId = (string?)null }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
        => FootballTimelinePackage.GetDefinitionsJson();

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "GOALS",
                name = "Goals",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "ASSISTS",
                name = "Assists",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "YELLOW_CARDS",
                name = "Yellow Cards",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "RED_CARDS",
                name = "Red Cards",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SHOTS",
                name = "Shots",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SHOTS_ON_TARGET",
                name = "Shots on Target",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "POSSESSION",
                name = "Possession %",
                type = "Percentage",
                aggregation = "Average",
                displayFormat = "P0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PASSES",
                name = "Passes",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PASS_ACCURACY",
                name = "Pass Accuracy %",
                type = "Percentage",
                aggregation = "Average",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FOULS",
                name = "Fouls",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "OFFSIDES",
                name = "Offsides",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "CORNERS",
                name = "Corners",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SAVES",
                name = "Saves",
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
                type = "WinLossDraw",
                pointsAwarded = 3,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "DRAW",
                name = "Draw",
                type = "WinLossDraw",
                pointsAwarded = 1,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "LOSS",
                name = "Loss",
                type = "WinLossDraw",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "WIN_EXTRA_TIME",
                name = "Win in Extra Time",
                type = "WinLossDraw",
                pointsAwarded = 3,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "WIN_PENALTIES",
                name = "Win on Penalties",
                type = "WinLossDraw",
                pointsAwarded = 2,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "LOSS_EXTRA_TIME",
                name = "Loss in Extra Time",
                type = "WinLossDraw",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "LOSS_PENALTIES",
                name = "Loss on Penalties",
                type = "WinLossDraw",
                pointsAwarded = 1,
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
                criteria = "Points",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 2,
                criteria = "GoalDifference",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 3,
                criteria = "GoalsFor",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 4,
                criteria = "HeadToHead",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 5,
                criteria = "FairPlay",
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
                showGoals = true,
                showCards = true,
                showPossession = true,
                showShots = true,
                showShotsOnTarget = true
            },
            timeline = new
            {
                showGoals = true,
                showCards = true,
                showSubstitutions = true,
                showMilestones = true
            },
            standings = new
            {
                showPlayed = true,
                showWon = true,
                showDrawn = true,
                showLost = true,
                showGoalsFor = true,
                showGoalsAgainst = true,
                showGoalDifference = true,
                showPoints = true
            },
            colors = new
            {
                homeTeam = "#1E88E5",
                awayTeam = "#E53935",
                win = "#4CAF50",
                draw = "#FFC107",
                loss = "#F44336"
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
                allowTies = true,
                maxExtraTimeMinutes = 30,
                requiresPenaltyShootout = true
            },
            participants = new
            {
                minRosterSize = 11,
                maxRosterSize = 25,
                minOnField = 7,
                maxOnField = 11,
                maxSubstitutions = 5,
                allowUnlimitedSubstitutions = false
            },
            events = new
            {
                allowNegativeScore = false,
                allowScoreAfterFinalWhistle = false,
                validateMinuteSequence = true,
                maxEventsPerMinute = 10
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = true,
                allowClockStoppage = true,
                allowEarlyStart = false
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
