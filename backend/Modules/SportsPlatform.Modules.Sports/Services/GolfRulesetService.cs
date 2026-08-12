using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface IGolfRulesetService
{
    Task<SportDefinitionVersion> ConfigureGolfRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetGolfRulesetAsync(CancellationToken cancellationToken = default);
}

public class GolfRulesetService : IGolfRulesetService
{
    private readonly ISportsService _sportsService;
    private const string GolfCode = "GOLF";
    public static readonly int[] StandardHolePars =
        { 4, 4, 3, 5, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4 };

    public GolfRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureGolfRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // Get or create golf sport
        var golf = await _sportsService.GetSportByCodeAsync(GolfCode, cancellationToken);
        if (golf == null)
        {
            golf = await _sportsService.CreateSportAsync(
                GolfCode,
                "Golf",
                "Golf - Individual stroke play tournament with multiple rounds",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            golf.Id,
            "Standard Rules v1.0",
            createdBy,
            cancellationToken);

        // Configure the version with golf-specific rules
        version.ConfigureContestShape(
            contestShape: "Individual",
            participantType: "Athlete",
            segmentDefinitions: GetSegmentDefinitions(),
            eventDefinitions: GetEventDefinitions(),
            metricDefinitions: GetMetricDefinitions(),
            resultMethods: GetResultMethods(),
            rankingRules: GetRankingRules(),
            displayConfiguration: GetDisplayConfiguration(),
            validationConfiguration: GetValidationConfiguration()
        );

        await _sportsService.UpdateSportAsync(golf, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetGolfRulesetAsync(CancellationToken cancellationToken = default)
    {
        var golf = await _sportsService.GetSportByCodeAsync(GolfCode, cancellationToken);
        if (golf == null) return null;

        // Get the latest published version
        return golf.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        object Round(string id, string name, int order, bool optional, string? next) => new
        {
            id,
            name,
            type = "Round",
            order,
            durationMinutes = 240,
            hasStoppageTime = false,
            stoppageMinutes = 0,
            isOptional = optional,
            nextSegmentId = next,
            nestedSegments = StandardHolePars.Select((par, index) => new
            {
                id = $"{id}_HOLE_{index + 1}",
                name = $"Hole {index + 1}",
                type = "Hole",
                order = index + 1,
                par,
                isOptional = false
            })
        };
        var segments = new object[]
        {
            Round("ROUND_1", "Round 1", 1, false, "ROUND_2"),
            Round("ROUND_2", "Round 2", 2, false, "CUT"),
            new { id = "CUT", name = "Cut", type = "Break", order = 3, durationMinutes = 0, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "ROUND_3" },
            Round("ROUND_3", "Round 3", 4, true, "ROUND_4"),
            Round("ROUND_4", "Round 4", 5, true, null)
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
    {
        var events = new object[]
        {
            new { id = "STROKES", name = "Hole Strokes", category = "Scoring", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minStrokes = 1, maxStrokes = 20, requiresHole = true } },
            new { id = "PENALTY_STROKE", name = "Penalty Stroke", category = "Penalty", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPenaltyStrokes = 1, maxPenaltyStrokes = 20, requiresHole = true } },
            // Scoring events
            new { id = "HOLE_SCORE_3", name = "Hole Score - 3", category = "Scoring", points = 3, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPar = -4, maxPar = 0 } },
            new { id = "HOLE_SCORE_4", name = "Hole Score - 4", category = "Scoring", points = 4, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPar = -3, maxPar = 1 } },
            new { id = "HOLE_SCORE_5", name = "Hole Score - 5", category = "Scoring", points = 5, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPar = -2, maxPar = 2 } },
            new { id = "HOLE_SCORE_6", name = "Hole Score - 6", category = "Scoring", points = 6, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPar = -1, maxPar = 3 } },
            new { id = "HOLE_SCORE_7", name = "Hole Score - 7+", category = "Scoring", points = 7, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minPar = 0, maxPar = 10 } },
            
            // Special scoring
            new { id = "EAGLE", name = "Eagle", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = -2 } },
            new { id = "BIRDIE", name = "Birdie", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = -1 } },
            new { id = "PAR", name = "Par", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = 0 } },
            new { id = "BOGEY", name = "Bogey", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = 1 } },
            new { id = "DOUBLE_BOGEY", name = "Double Bogey", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = 2 } },
            new { id = "TRIPLE_BOGEY_PLUS", name = "Triple Bogey+", category = "Special", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { relativeToPar = 3 } },
            
            // Penalties
            new { id = "WATER_HAZARD", name = "Water Hazard Penalty", category = "Penalty", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { penaltyStrokes = 1 } },
            new { id = "OUT_OF_BOUNDS", name = "Out of Bounds Penalty", category = "Penalty", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { penaltyStrokes = 1 } },
            new { id = "LOST_BALL", name = "Lost Ball Penalty", category = "Penalty", points = 1, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { penaltyStrokes = 1 } },
            new { id = "BUNKER", name = "Bunker", category = "Situation", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "FAIRWAY", name = "Fairway Hit", category = "Situation", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "GREEN_IN_REGULATION", name = "Green in Regulation", category = "Situation", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // Round completion
            new { id = "ROUND_COMPLETE", name = "Round Complete", category = "Timing", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = false, requiresAssist = false, validation = new { } },
            new { id = "CUT_MADE", name = "Cut Made", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "CUT_MISSED", name = "Cut Missed", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "WD", name = "Withdrawal", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "DQ", name = "Disqualification", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } }
        };

        return JsonSerializer.Serialize(events);
    }

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "PAR",
                name = "Par",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PENALTY_STROKES",
                name = "Penalty Strokes",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TOTAL_STROKES",
                name = "Total Strokes",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "TO_PAR",
                name = "To Par",
                type = "Integer",
                aggregation = "Calculated",
                displayFormat = "+N0;-N0;0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "EAGLES",
                name = "Eagles",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BIRDIES",
                name = "Birdies",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PARS",
                name = "Pars",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BOGEYS",
                name = "Bogeys",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "DOUBLE_BOGEYS",
                name = "Double Bogeys+",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FAIRWAYS_HIT",
                name = "Fairways Hit",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "FAIRWAY_PERCENTAGE",
                name = "Fairway %",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "GREENS_IN_REGULATION",
                name = "Greens in Regulation",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "GIR_PERCENTAGE",
                name = "GIR %",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PUTTS",
                name = "Putts",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PUTTS_PER_HOLE",
                name = "Putts/Hole",
                type = "Decimal",
                aggregation = "Average",
                displayFormat = "N2",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SAND_SAVES",
                name = "Sand Saves",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PENALTIES",
                name = "Penalties",
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
                id = "WINNER",
                name = "Winner",
                type = "Ranked",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "SECOND",
                name = "Second Place",
                type = "Ranked",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "THIRD",
                name = "Third Place",
                type = "Ranked",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "TIE",
                name = "Tie",
                type = "Tied",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "CUT",
                name = "Cut",
                type = "Eliminated",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "WD",
                name = "Withdrawal",
                type = "Withdrawn",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "DQ",
                name = "Disqualification",
                type = "Disqualified",
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
                criteria = "TotalStrokes",
                direction = "Ascending",
                tiebreaker = true,
                recalculateAfter = "EachHoleOrRound"
            },
            new
            {
                order = 2,
                criteria = "LastRoundScore",
                direction = "Ascending",
                tiebreaker = true,
                recalculateAfter = "EachHoleOrRound"
            },
            new
            {
                order = 3,
                criteria = "BackNineScore",
                direction = "Ascending",
                tiebreaker = true,
                recalculateAfter = "EachHoleOrRound"
            },
            new
            {
                order = 4,
                criteria = "BirdieOrBetter",
                direction = "Descending",
                tiebreaker = true,
                recalculateAfter = "EachHoleOrRound"
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
                showTotalStrokes = true,
                showToPar = true,
                showRoundByRound = true,
                showPosition = true,
                showCourse = true,
                showHolePar = true
            },
            timeline = new
            {
                showHoleScores = true,
                showSpecialScores = true,
                showCumulativeScore = true
            },
            standings = new
            {
                showPosition = true,
                showName = true,
                showTotalStrokes = true,
                showToPar = true,
                showRound1 = true,
                showRound2 = true,
                showRound3 = true,
                showRound4 = true
            },
            colors = new
            {
                leader = "#FFD700",
                cutLine = "#FF6B6B",
                madeCut = "#4CAF50",
                missedCut = "#F44336",
                withdrawn = "#9E9E9E"
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
                maxParticipants = 200,
                allowTies = true,
                maxRounds = 4,
                minRoundsForOfficial = 2,
                cutAfterRound = 2,
                cutToPlayers = 70,
                allowCut = true
            },
            course = new
            {
                requireCourseName = true,
                requireCourseLocation = true,
                holesPerRound = 18,
                allowedParValues = new[] { 3, 4, 5 },
                requireHoleParsToMatchRoundPar = true,
                standardHolePars = StandardHolePars,
                standardParPerRound = StandardHolePars.Sum()
            },
            ranking = new
            {
                direction = "LowerIsBetter",
                recalculateAfterEachHole = true,
                recalculateAfterEachRound = true,
                allowTies = true
            },
            cut = new
            {
                configurable = true,
                defaultAfterRound = 2,
                defaultToPlayers = 70,
                includeTiesAtLine = true
            },
            participants = new
            {
                minRosterSize = 1,
                maxRosterSize = 1,
                minOnField = 1,
                maxOnField = 1,
                maxSubstitutions = 0,
                allowUnlimitedSubstitutions = false
            },
            events = new
            {
                allowNegativeScore = false,
                allowScoreAfterFinalWhistle = false,
                validateHoleSequence = true,
                maxStrokesPerHole = 15
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = false,
                allowClockStoppage = false,
                allowEarlyStart = true,
                roundTimeLimit = 240
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
