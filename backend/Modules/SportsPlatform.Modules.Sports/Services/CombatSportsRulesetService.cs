using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface ICombatSportsRulesetService
{
    Task<SportDefinitionVersion> ConfigureCombatSportsRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetCombatSportsRulesetAsync(CancellationToken cancellationToken = default);
}

public class CombatSportsRulesetService : ICombatSportsRulesetService
{
    private readonly ISportsService _sportsService;
    private const string CombatSportsCode = "MMA";

    public CombatSportsRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureCombatSportsRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
    {
        // Get or create combat sports sport
        var combatSports = await _sportsService.GetSportByCodeAsync(CombatSportsCode, cancellationToken);
        if (combatSports == null)
        {
            combatSports = await _sportsService.CreateSportAsync(
                CombatSportsCode,
                "Combat Sports",
                "MMA and Boxing - Combat sports with weight classes and various result methods",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            combatSports.Id,
            "Standard Combat Rules",
            createdBy,
            cancellationToken);

        // Configure the version with combat sports-specific rules
        version.ConfigureContestShape(
            contestShape: "FighterVsFighter",
            participantType: "Athlete",
            segmentDefinitions: GetSegmentDefinitions(),
            eventDefinitions: GetEventDefinitions(),
            metricDefinitions: GetMetricDefinitions(),
            resultMethods: GetResultMethods(),
            rankingRules: GetRankingRules(),
            displayConfiguration: GetDisplayConfiguration(),
            validationConfiguration: GetValidationConfiguration()
        );

        await _sportsService.UpdateSportAsync(combatSports, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetCombatSportsRulesetAsync(CancellationToken cancellationToken = default)
    {
        var combatSports = await _sportsService.GetSportByCodeAsync(CombatSportsCode, cancellationToken);
        if (combatSports == null) return null;

        // Get the latest published version
        return combatSports.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions()
    {
        var segments = new object[]
        {
            // Boxing segments (12 rounds typical)
            new { id = "B_RD1", name = "Boxing Round 1", type = "Round", order = 1, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD2" },
            new { id = "B_RD2", name = "Boxing Round 2", type = "Round", order = 2, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD3" },
            new { id = "B_RD3", name = "Boxing Round 3", type = "Round", order = 3, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD4" },
            new { id = "B_RD4", name = "Boxing Round 4", type = "Round", order = 4, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD5" },
            new { id = "B_RD5", name = "Boxing Round 5", type = "Round", order = 5, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD6" },
            new { id = "B_RD6", name = "Boxing Round 6", type = "Round", order = 6, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD7" },
            new { id = "B_RD7", name = "Boxing Round 7", type = "Round", order = 7, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD8" },
            new { id = "B_RD8", name = "Boxing Round 8", type = "Round", order = 8, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD9" },
            new { id = "B_RD9", name = "Boxing Round 9", type = "Round", order = 9, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD10" },
            new { id = "B_RD10", name = "Boxing Round 10", type = "Round", order = 10, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD11" },
            new { id = "B_RD11", name = "Boxing Round 11", type = "Round", order = 11, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "B_RD12" },
            new { id = "B_RD12", name = "Boxing Round 12", type = "Round", order = 12, durationMinutes = 3, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = (string?)null },
            
            // MMA segments (5 rounds typical)
            new { id = "M_RD1", name = "MMA Round 1", type = "Round", order = 1, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "M_RD2" },
            new { id = "M_RD2", name = "MMA Round 2", type = "Round", order = 2, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "M_RD3" },
            new { id = "M_RD3", name = "MMA Round 3", type = "Round", order = 3, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "M_RD4" },
            new { id = "M_RD4", name = "MMA Round 4", type = "Round", order = 4, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "M_RD5" },
            new { id = "M_RD5", name = "MMA Round 5", type = "Round", order = 5, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = (string?)null }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions()
    {
        var events = new object[]
        {
            // Striking events
            new { id = "PUNCH_LANDED", name = "Punch Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "JAB_LANDED", name = "Jab Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "CROSS_LANDED", name = "Cross Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "HOOK_LANDED", name = "Hook Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "UPPER_CUT_LANDED", name = "Uppercut Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "KICK_LANDED", name = "Kick Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "KNEE_LANDED", name = "Knee Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "ELBOW_LANDED", name = "Elbow Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "HEAD_STRIKE_LANDED", name = "Head Strike Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "BODY_STRIKE_LANDED", name = "Body Strike Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "LEG_STRIKE_LANDED", name = "Leg Strike Landed", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Significant strikes (MMA)
            new { id = "SIGNIFICANT_STRIKE", name = "Significant Strike", category = "Striking", points = 1, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Grappling events
            new { id = "TAKEDOWN_LANDED", name = "Takedown Landed", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "TAKEDOWN_ATTEMPTED", name = "Takedown Attempted", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "CLINCH", name = "Clinch", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "SUBMISSION_ATTEMPT", name = "Submission Attempt", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "SWEEP", name = "Sweep", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "GROUND_CONTROL", name = "Ground Control", category = "Grappling", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Knockdown events
            new { id = "KNOCKDOWN", name = "Knockdown", category = "Knockdown", points = 0, isPrimary = true, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Defense events
            new { id = "STRIKE_DEFENDED", name = "Strike Defended", category = "Defense", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "TAKEDOWN_DEFENDED", name = "Takedown Defended", category = "Defense", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Fouls
            new { id = "FOUL", name = "Foul", category = "Foul", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "LOW_BLOW", name = "Low Blow", category = "Foul", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            new { id = "EYE_POKE", name = "Eye Poke", category = "Foul", points = 0, isPrimary = false, requiresMinute = true, requiresParticipant = true, requiresAssist = false, validation = new { minMinute = 0, maxMinute = 60 } },
            
            // Round scoring (boxing)
            new { id = "ROUND_WIN", name = "Round Win", category = "Scoring", points = 10, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } },
            new { id = "ROUND_LOSS", name = "Round Loss", category = "Scoring", points = 9, isPrimary = false, requiresMinute = false, requiresParticipant = true, requiresAssist = false, validation = new { } }
        };

        return JsonSerializer.Serialize(events);
    }

    public static string GetMetricDefinitions()
    {
        var metrics = new[]
        {
            new
            {
                id = "TOTAL_STRIKES_LANDED",
                name = "Total Strikes Landed",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "SIGNIFICANT_STRIKES_LANDED",
                name = "Significant Strikes Landed",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "STRIKE_ACCURACY",
                name = "Strike Accuracy",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TAKEDOWNS_LANDED",
                name = "Takedowns Landed",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TAKEDOWN_ACCURACY",
                name = "Takedown Accuracy",
                type = "Percentage",
                aggregation = "Calculated",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "KNOCKDOWNS",
                name = "Knockdowns",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "CONTROL_TIME",
                name = "Control Time (seconds)",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SUBMISSION_ATTEMPTS",
                name = "Submission Attempts",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "REVERSALS",
                name = "Reversals",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
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
                id = "ROUND_SCORE",
                name = "Round Score",
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
            // KO/TKO results
            new
            {
                id = "KO",
                name = "Knockout",
                type = "Knockout",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "TKO_DOCTOR_STOP",
                name = "TKO - Doctor Stoppage",
                type = "TechnicalKnockout",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "TKO_CORNER_STOPPAGE",
                name = "TKO - Corner Stoppage",
                type = "TechnicalKnockout",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "TKO_REF_STOPPAGE",
                name = "TKO - Referee Stoppage",
                type = "TechnicalKnockout",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            
            // Submission results
            new
            {
                id = "SUBMISSION",
                name = "Submission",
                type = "Submission",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "SUBMISSION_STRIKES",
                name = "Submission by Strikes",
                type = "Submission",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            
            // Decision results
            new
            {
                id = "DECISION_UNANIMOUS",
                name = "Decision - Unanimous",
                type = "Decision",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "DECISION_SPLIT",
                name = "Decision - Split",
                type = "Decision",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "DECISION_MAJORITY",
                name = "Decision - Majority",
                type = "Decision",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            
            // Draw results
            new
            {
                id = "DRAW",
                name = "Draw",
                type = "Draw",
                pointsAwarded = 0,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "DRAW_SPLIT",
                name = "Draw - Split",
                type = "Draw",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            
            // Other results
            new
            {
                id = "NO_CONTEST",
                name = "No Contest",
                type = "NoContest",
                pointsAwarded = 0,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "DISQUALIFICATION",
                name = "Disqualification",
                type = "Disqualification",
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
                criteria = "WinLossRecord",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 2,
                criteria = "FinishRate",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 3,
                criteria = "RecentResults",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 4,
                criteria = "StrengthOfSchedule",
                direction = "Descending",
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
                showFighterNames = true,
                showRound = true,
                showTimeRemaining = true,
                showResult = true,
                showMethod = true
            },
            timeline = new
            {
                showStrikes = true,
                showTakedowns = true,
                showKnockdowns = true,
                showSubmissions = true,
                showFouls = true
            },
            standings = new
            {
                showRank = true,
                showName = true,
                showRecord = true,
                showFinishRate = true,
                showLastFight = true
            },
            colors = new
            {
                redCorner = "#F44336",
                blueCorner = "#2196F3",
                winner = "#4CAF50",
                loser = "#F44336",
                draw = "#FFC107"
            },
            weightClasses = new[]
            {
                new { name = "Strawweight", maxWeight = 115, unit = "lbs" },
                new { name = "Flyweight", maxWeight = 125, unit = "lbs" },
                new { name = "Bantamweight", maxWeight = 135, unit = "lbs" },
                new { name = "Featherweight", maxWeight = 145, unit = "lbs" },
                new { name = "Lightweight", maxWeight = 155, unit = "lbs" },
                new { name = "Welterweight", maxWeight = 170, unit = "lbs" },
                new { name = "Middleweight", maxWeight = 185, unit = "lbs" },
                new { name = "Light Heavyweight", maxWeight = 205, unit = "lbs" },
                new { name = "Heavyweight", maxWeight = 265, unit = "lbs" },
                new { name = "Super Heavyweight", maxWeight = 999, unit = "lbs" }
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
                maxRounds = 12,
                requiresDecision = true,
                allowNoContest = true
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
                validateMinuteSequence = true,
                maxEventsPerMinute = 50
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = true,
                allowClockStoppage = true,
                allowEarlyStart = false,
                roundTimeLimit = 3
            },
            weightClasses = new
            {
                requireWeightIn = true,
                allowCatchWeight = false,
                hydrationRules = true
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
