using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface IAthleticsRulesetService
{
    Task<SportDefinitionVersion> ConfigureAthleticsRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetAthleticsRulesetAsync(CancellationToken cancellationToken = default);
}

public class AthleticsRulesetService : IAthleticsRulesetService
{
    private readonly ISportsService _sportsService;
    private const string AthleticsCode = "ATHLETICS";

    public AthleticsRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureAthleticsRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // Get or create athletics sport
        var athletics = await _sportsService.GetSportByCodeAsync(AthleticsCode, cancellationToken);
        if (athletics == null)
        {
            athletics = await _sportsService.CreateSportAsync(
                AthleticsCode,
                "Athletics",
                "Track and Field - Includes sprints, distance, jumps, throws, and combined events",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            athletics.Id,
            "Standard Athletics Rules",
            createdBy,
            cancellationToken);

        // Configure the version with athletics-specific rules
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

        await _sportsService.UpdateSportAsync(athletics, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetAthleticsRulesetAsync(CancellationToken cancellationToken = default)
    {
        var athletics = await _sportsService.GetSportByCodeAsync(AthleticsCode, cancellationToken);
        if (athletics == null) return null;

        // Get the latest published version
        return athletics.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        var segments = new object[]
        {
            // Track events
            new { id = "HEAT_1", name = "Heat 1", type = "Heat", order = 1, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SEMI_1" },
            new { id = "HEAT_2", name = "Heat 2", type = "Heat", order = 2, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SEMI_1" },
            new { id = "HEAT_3", name = "Heat 3", type = "Heat", order = 3, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "SEMI_1" },
            new { id = "SEMI_1", name = "Semi-Final 1", type = "Heat", order = 4, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "FINAL" },
            new { id = "SEMI_2", name = "Semi-Final 2", type = "Heat", order = 5, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "FINAL" },
            new { id = "FINAL", name = "Final", type = "Final", order = 6, durationMinutes = 10, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = (string?)null },
            
            // Field events
            new { id = "QUALIFICATION", name = "Qualification Round", type = "Round", order = 1, durationMinutes = 60, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "FINAL" },
            new { id = "GROUP_A", name = "Group A", type = "Group", order = 2, durationMinutes = 60, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "FINAL" },
            new { id = "GROUP_B", name = "Group B", type = "Group", order = 3, durationMinutes = 60, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "FINAL" }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
    {
        var events = new object[]
        {
            // Track events - Sprints
            new { id = "FINISH", name = "Finish", category = "Timing", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minTime = 0, maxTime = 120, unit = "seconds" } },
            new { id = "FALSE_START", name = "False Start", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { disqualification = true } },
            new { id = "DNS", name = "Did Not Start", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "DNF", name = "Did Not Finish", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "DQ", name = "Disqualification", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // Hurdles specific
            new { id = "HURDLE_CLEAR", name = "Hurdle Cleared", category = "Technical", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "HURDLE_BLOWN", name = "Hurdle Blown", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { penaltyHurdles = 1 } },
            
            // Distance events
            new { id = "LAP_COMPLETE", name = "Lap Complete", category = "Timing", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "PACKING", name = "Pacing/Packing", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { disqualification = true } },
            
            // Relays
            new { id = "BATON_PASS", name = "Baton Pass", category = "Technical", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = true, validation = new { zoneRequired = true } },
            new { id = "BATON_DROP", name = "Baton Drop", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "EARLY_PASS", name = "Early Pass", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { disqualification = true } },
            
            // Jumps
            new { id = "JUMP_ATTEMPT", name = "Jump Attempt", category = "Technical", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "JUMP_NO_MARK", name = "No Mark (Foul)", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "JUMP_VALID", name = "Valid Jump", category = "Scoring", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minDistance = 0, maxDistance = 100, unit = "meters" } },
            new { id = "FOUL", name = "Foul", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // High Jump / Pole Vault
            new { id = "CLEARANCE", name = "Clearance", category = "Technical", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minHeight = 0, maxHeight = 10, unit = "meters" } },
            new { id = "FAILURE", name = "Failure", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { attemptsUsed = 1 } },
            new { id = "HEIGHT_PASSED", name = "Height Passed", category = "Status", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // Throws
            new { id = "THROW_ATTEMPT", name = "Throw Attempt", category = "Technical", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "THROW_VALID", name = "Valid Throw", category = "Scoring", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { minDistance = 0, maxDistance = 100, unit = "meters" } },
            new { id = "FOUL_THROW", name = "Foul Throw", category = "Penalty", points = 0, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            
            // Combined events
            new { id = "POINTS_EARNED", name = "Points Earned", category = "Scoring", points = 0, isPrimary = true, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } }
        };

        return JsonSerializer.Serialize(events);
    }

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "FINISH_TIME",
                name = "Finish Time",
                type = "Duration",
                aggregation = "Min",
                displayFormat = "mm\\:ss\\.ff",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "PLACE",
                name = "Place",
                type = "Integer",
                aggregation = "Min",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "DISTANCE",
                name = "Distance",
                type = "Decimal",
                aggregation = "Max",
                displayFormat = "0.00 m",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "HEIGHT",
                name = "Height",
                type = "Decimal",
                aggregation = "Max",
                displayFormat = "0.00 m",
                showInStandings = true,
                showInContest = true
            },
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
                id = "WIND",
                name = "Wind",
                type = "Decimal",
                aggregation = "None",
                displayFormat = "+0.0 m/s",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "REACTION_TIME",
                name = "Reaction Time",
                type = "Decimal",
                aggregation = "None",
                displayFormat = "0.000 s",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "ATTEMPTS",
                name = "Attempts",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "BEST_EFFORT",
                name = "Best Effort",
                type = "Decimal",
                aggregation = "Max",
                displayFormat = "0.00 m",
                showInStandings = true,
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
                id = "QUALIFIED",
                name = "Qualified",
                type = "Qualified",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "QUALIFIED_BY_TIME",
                name = "Qualified by Time",
                type = "Qualified",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "DNS",
                name = "Did Not Start",
                type = "DNS",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "DNF",
                name = "Did Not Finish",
                type = "DNF",
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
            },
            new
            {
                id = "NM",
                name = "No Mark",
                type = "NM",
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
            // For time-based events (lower is better)
            new
            {
                order = 1,
                criteria = "FinishTime",
                direction = "Ascending",
                tiebreaker = true
            },
            // For distance/height events (higher is better)
            new
            {
                order = 1,
                criteria = "Distance",
                direction = "Descending",
                tiebreaker = true
            },
            // For combined events
            new
            {
                order = 1,
                criteria = "TotalPoints",
                direction = "Descending",
                tiebreaker = true
            },
            // Tiebreakers
            new
            {
                order = 2,
                criteria = "ReactionTime",
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
                showPlace = true,
                showName = true,
                showTime = true,
                showWind = true,
                showReactionTime = true,
                showRecordMark = true
            },
            timeline = new
            {
                showLiveUpdates = true,
                showQualificationStatus = true,
                showPersonalBests = true
            },
            standings = new
            {
                showPosition = true,
                showName = true,
                showTime = true,
                showDistance = true,
                showQualification = true,
                showNationalRecord = true,
                showPersonalBest = true
            },
            colors = new
            {
                leader = "#FFD700",
                qualified = "#4CAF50",
                notQualified = "#F44336",
                personalBest = "#2196F3",
                seasonBest = "#9C27B0",
                nationalRecord = "#E91E63",
                worldRecord = "#FF5722"
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
                maxParticipants = 100,
                allowTies = true,
                maxHeats = 10,
                maxRounds = 3,
                qualificationSlots = 24,
                qualifyByPlace = 3,
                qualifyByTime = true
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
                allowNegativeTime = false,
                maxWind = 2.0,
                minReactionTime = 0.100,
                maxReactionTime = 10.0,
                validateLane = true,
                validateZone = true
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = false,
                allowClockStoppage = false,
                allowEarlyStart = false,
                useFalseStartDetection = true,
                usePhotoFinish = true
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
