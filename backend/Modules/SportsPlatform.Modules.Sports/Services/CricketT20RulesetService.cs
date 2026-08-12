using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface ICricketT20RulesetService
{
    Task<SportDefinitionVersion> ConfigureCricketT20RulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetCricketT20RulesetAsync(CancellationToken cancellationToken = default);
}

public class CricketT20RulesetService : ICricketT20RulesetService
{
    private readonly ISportsService _sportsService;
    private const string CricketCode = "CRICKET";

    public CricketT20RulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureCricketT20RulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // T20 is the controlled ruleset for the canonical seeded cricket sport.
        var cricket = await _sportsService.GetSportByCodeAsync(CricketCode, cancellationToken);
        if (cricket == null)
        {
            cricket = await _sportsService.CreateSportAsync(
                CricketCode,
                "Cricket",
                "Twenty20 Cricket - Limited overs format with 20 overs per side",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            cricket.Id,
            "Standard T20 Rules",
            createdBy,
            cancellationToken);

        // Configure the version with T20 cricket-specific rules
        version.ConfigureContestShape(
            contestShape: "Team",
            participantType: "Team",
            segmentDefinitions: GetSegmentDefinitions(),
            eventDefinitions: GetEventDefinitions(),
            metricDefinitions: GetMetricDefinitions(),
            resultMethods: GetResultMethods(),
            rankingRules: GetRankingRules(),
            displayConfiguration: GetDisplayConfiguration(),
            validationConfiguration: GetValidationConfiguration()
        );

        await _sportsService.UpdateSportAsync(cricket, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetCricketT20RulesetAsync(CancellationToken cancellationToken = default)
    {
        var cricket = await _sportsService.GetSportByCodeAsync(CricketCode, cancellationToken);
        if (cricket == null) return null;

        return cricket.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        var segments = new object[]
        {
            new { id = "TOSS", name = "Toss", type = "Event", order = 0, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "POWERPLAY_1" },
            new { id = "POWERPLAY_1", name = "Powerplay 1 (Over 1-6)", type = "InningsPhase", order = 1, durationMinutes = 30, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "MIDDLE_OVER" },
            new { id = "MIDDLE_OVER", name = "Middle Overs (Over 7-15)", type = "InningsPhase", order = 2, durationMinutes = 50, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "DEATH_OVER" },
            new { id = "DEATH_OVER", name = "Death Overs (Over 16-20)", type = "InningsPhase", order = 3, durationMinutes = 30, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "INNINGS_BREAK" },
            new { id = "INNINGS_BREAK", name = "Innings Break", type = "Break", order = 4, durationMinutes = 15, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "INNINGS2_POWERPLAY_1" },
            
            // Second innings
            new { id = "INNINGS2_POWERPLAY_1", name = "Powerplay 1 (Over 1-6)", type = "InningsPhase", order = 5, durationMinutes = 30, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "INNINGS2_MIDDLE_OVER" },
            new { id = "INNINGS2_MIDDLE_OVER", name = "Middle Overs (Over 7-15)", type = "InningsPhase", order = 6, durationMinutes = 50, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "INNINGS2_DEATH_OVER" },
            new { id = "INNINGS2_DEATH_OVER", name = "Death Overs (Over 16-20)", type = "InningsPhase", order = 7, durationMinutes = 30, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SUPER_OVER" },
            new { id = "SUPER_OVER", name = "Super Over", type = "TieBreaker", order = 8, durationMinutes = 15, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = (string?)null }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
    {
        var events = new object[]
        {
            // Ball outcomes
            new { id = "RUN_0", name = "Dot Ball", category = "Scoring", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 0 } },
            new { id = "RUN_1", name = "Single", category = "Scoring", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 1 } },
            new { id = "RUN_2", name = "Double", category = "Scoring", points = 2, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 2 } },
            new { id = "RUN_3", name = "Triple", category = "Scoring", points = 3, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 3 } },
            new { id = "RUN_4", name = "Four", category = "Scoring", points = 4, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 4, boundary = true } },
            new { id = "RUN_6", name = "Six", category = "Scoring", points = 6, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 6, boundary = true } },
            
            // Dismissals
            new { id = "BOWLED", name = "Bowled", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { dismissalType = "bowled" } },
            new { id = "CAUGHT", name = "Caught", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = true, validation = new { dismissalType = "caught" } },
            new { id = "LBW", name = "Leg Before Wicket", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { dismissalType = "lbw" } },
            new { id = "RUN_OUT", name = "Run Out", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = true, validation = new { dismissalType = "runOut" } },
            new { id = "STUMPED", name = "Stumped", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = true, validation = new { dismissalType = "stumped" } },
            new { id = "HIT_WICKET", name = "Hit Wicket", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { dismissalType = "hitWicket" } },
            new { id = "HANDLED_BALL", name = "Handled the Ball", category = "Dismissal", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { dismissalType = "handledBall" } },
            
            // Extras
            new { id = "WIDE", name = "Wide", category = "Extra", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { extraType = "wide", additionalRun = true } },
            new { id = "NO_BALL", name = "No Ball", category = "Extra", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { extraType = "noBall", additionalRun = true } },
            new { id = "BYE", name = "Bye", category = "Extra", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { extraType = "bye" } },
            new { id = "LEG_BYE", name = "Leg Bye", category = "Extra", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { extraType = "legBye" } },
            
            // Penalty events
            new { id = "PENALTY_RUNS", name = "Penalty Runs", category = "Penalty", points = 5, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { penaltyRuns = 5 } },
            
            // Over events
            new { id = "OVER_COMPLETE", name = "Over Complete", category = "Timing", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { balls = 6 } },
            new { id = "INNINGS_END", name = "Innings End", category = "Timing", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { } },
            new { id = "MATCH_END", name = "Match End", category = "Timing", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { } },
            
            // Match events
            new { id = "TOSS_WON", name = "Toss Won", category = "Match", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "ELECTED_TO", name = "Elected To", category = "Match", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // Milestones
            new { id = "HALF_CENTURY", name = "Half Century", category = "Milestone", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 50 } },
            new { id = "CENTURY", name = "Century", category = "Milestone", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { runs = 100 } },
            new { id = "FIFTY_WICKETS", name = "50 Wickets", category = "Milestone", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { wickets = 50 } }
        };

        return JsonSerializer.Serialize(events);
    }

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "RUNS",
                name = "Runs",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "WICKETS",
                name = "Wickets",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "OVERS",
                name = "Overs",
                type = "Decimal",
                aggregation = "None",
                displayFormat = "0.0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "RUN_RATE",
                name = "Run Rate",
                type = "Decimal",
                aggregation = "Calculated",
                displayFormat = "0.00",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "ECONOMY",
                name = "Economy",
                type = "Decimal",
                aggregation = "Calculated",
                displayFormat = "0.00",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BALLS_FACED",
                name = "Balls Faced",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "STRIKE_RATE",
                name = "Strike Rate",
                type = "Decimal",
                aggregation = "Calculated",
                displayFormat = "0.00",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BALLS_BOWLED",
                name = "Balls Bowled",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "EXTRAS",
                name = "Extras",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "WIDES",
                name = "Wides",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "NO_BALLS",
                name = "No Balls",
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
                type = "Ranked",
                pointsAwarded = 2,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "LOSS",
                name = "Loss",
                type = "Ranked",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "TIE",
                name = "Tie",
                type = "Tied",
                pointsAwarded = 1,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "NO_RESULT",
                name = "No Result",
                type = "NoResult",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "ABANDONED",
                name = "Abandoned",
                type = "Abandoned",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
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
                criteria = "NetRunRate",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 3,
                criteria = "HeadToHead",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 4,
                criteria = "BowlingEconomy",
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
                showTeamName = true,
                showRuns = true,
                showWickets = true,
                showOvers = true,
                showRunRate = true,
                showRequiredRate = true,
                showPartnership = true,
                showLastWicket = true
            },
            timeline = new
            {
                showBallByBall = true,
                showOverSummary = true,
                showPartnerships = true,
                showFallOfWickets = true
            },
            standings = new
            {
                showPosition = true,
                showTeam = true,
                showPlayed = true,
                showWon = true,
                showLost = true,
                showTied = true,
                showNR = true,
                showPoints = true,
                showNRR = true
            },
            colors = new
            {
                battingTeam = "#2196F3",
                bowlingTeam = "#4CAF50",
                powerplay = "#FF9800",
                deathOvers = "#F44336",
                milestone = "#9C27B0"
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
                oversPerInnings = 20,
                ballsPerOver = 6,
                maxOversPerBowler = 4,
                powerplayOvers = 6,
                deathOversStart = 16
            },
            participants = new
            {
                minRosterSize = 11,
                maxRosterSize = 15,
                minOnField = 11,
                maxOnField = 11,
                maxSubstitutions = 0,
                allowUnlimitedSubstitutions = false
            },
            events = new
            {
                allowNegativeRuns = false,
                maxRunsPerBall = 7,
                validateOverCount = true,
                validateBowlerOvers = true,
                validatePowerplay = true
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = true,
                allowClockStoppage = true,
                allowEarlyStart = false,
                inningsTimeLimit = 90,
                matchTimeLimit = 210
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
