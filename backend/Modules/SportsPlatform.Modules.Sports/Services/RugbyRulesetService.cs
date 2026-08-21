 using System.Text.Json;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public interface IRugbyRulesetService
{
    Task<SportDefinitionVersion> ConfigureRugbyRulesetAsync(string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion> ConfigureRugbyRulesetAsync(string createdBy, string variant, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetRugbyRulesetAsync(CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetRugbyRulesetAsync(string variant, CancellationToken cancellationToken = default);
}

public static class RugbyVariants
{
    public const string Fifteens = "fifteens";
    public const string Sevens = "sevens";

    public static string Normalize(string? variant) => variant?.Trim().ToLowerInvariant() switch
    {
        "7" or "7s" or "seven" or "sevens" => Sevens,
        "15" or "15s" or "fifteen" or "fifteens" or null or "" => Fifteens,
        _ => throw new ArgumentException("Rugby variant must be 'fifteens' or 'sevens'.", nameof(variant))
    };

    public static string? FromValidationConfiguration(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration)) return null;
        try
        {
            using var document = JsonDocument.Parse(configuration);
            return document.RootElement.TryGetProperty("variant", out var value)
                ? Normalize(value.GetString())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public class RugbyRulesetService : IRugbyRulesetService
{
    private readonly ISportsService _sportsService;
    private const string RugbyCode = "RUGBY";

    public RugbyRulesetService(ISportsService sportsService)
    {
        _sportsService = sportsService;
    }

    public async Task<SportDefinitionVersion> ConfigureRugbyRulesetAsync(string createdBy, CancellationToken cancellationToken = default)
        => await ConfigureRugbyRulesetAsync(createdBy, RugbyVariants.Fifteens, cancellationToken);

    public async Task<SportDefinitionVersion> ConfigureRugbyRulesetAsync(string createdBy, string variant, CancellationToken cancellationToken = default)
    {
        variant = RugbyVariants.Normalize(variant);
        // Get or create rugby sport
        var rugby = await _sportsService.GetSportByCodeAsync(RugbyCode, cancellationToken);
        if (rugby == null)
        {
            rugby = await _sportsService.CreateSportAsync(
                RugbyCode,
                "Rugby Union",
                "Rugby Union with first-class support for 15-a-side and sevens",
                cancellationToken);
        }

        // Create a new version
        var version = await _sportsService.CreateSportVersionAsync(
            rugby.Id,
            variant == RugbyVariants.Sevens ? "Rugby Sevens Rules v1.0" : "Rugby Union Fifteens Rules v1.0",
            createdBy,
            cancellationToken);

        // Configure the version with rugby-specific rules
        version.ConfigureContestShape(
            contestShape: "TeamVsTeam",
            participantType: "Team",
            segmentDefinitions: GetSegmentDefinitions(variant),
            eventDefinitions: GetEventDefinitions(variant),
            metricDefinitions: GetMetricDefinitions(),
            resultMethods: GetResultMethods(variant),
            rankingRules: GetRankingRules(),
            displayConfiguration: GetDisplayConfiguration(variant),
            validationConfiguration: GetValidationConfiguration(variant)
        );

        await _sportsService.UpdateSportAsync(rugby, cancellationToken);
        await _sportsService.PublishSportVersionAsync(version.Id, cancellationToken);

        return version;
    }

    public async Task<SportDefinitionVersion?> GetRugbyRulesetAsync(CancellationToken cancellationToken = default)
        => await GetRugbyRulesetAsync(RugbyVariants.Fifteens, cancellationToken);

    public async Task<SportDefinitionVersion?> GetRugbyRulesetAsync(string variant, CancellationToken cancellationToken = default)
    {
        variant = RugbyVariants.Normalize(variant);
        var rugby = await _sportsService.GetSportByCodeAsync(RugbyCode, cancellationToken);
        if (rugby == null) return null;

        return rugby.Versions
            .Where(v => v.IsPublished && RugbyVariants.FromValidationConfiguration(v.ValidationConfiguration) == variant)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
    }

    public static string GetSegmentDefinitions(string variant = RugbyVariants.Fifteens)
    {
        variant = RugbyVariants.Normalize(variant);
        if (variant == RugbyVariants.Sevens)
        {
            var sevensSegments = new object[]
            {
                new { id = "FIRST_HALF", name = "First Half", type = "Half", order = 1, durationMinutes = 7, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "HALF_TIME" },
                new { id = "HALF_TIME", name = "Half Time", type = "Break", order = 2, durationMinutes = 2, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SECOND_HALF" },
                new { id = "SECOND_HALF", name = "Second Half", type = "Half", order = 3, durationMinutes = 7, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "EXTRA_TIME" },
                new { id = "EXTRA_TIME", name = "Golden-point Extra Time", type = "ExtraTime", order = 4, durationMinutes = 5, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, nextSegmentId = (string?)null }
            };
            return JsonSerializer.Serialize(sevensSegments);
        }

        var segments = new object[]
        {
            new { id = "FIRST_HALF", name = "First Half", type = "Half", order = 1, durationMinutes = 40, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "HALF_TIME" },
            new { id = "HALF_TIME", name = "Half Time", type = "Break", order = 2, durationMinutes = 10, hasStoppageTime = false, stoppageMinutes = 0, isOptional = false, nextSegmentId = "SECOND_HALF" },
            new { id = "SECOND_HALF", name = "Second Half", type = "Half", order = 3, durationMinutes = 40, hasStoppageTime = true, stoppageMinutes = 0, isOptional = false, nextSegmentId = "EXTRA_TIME_FIRST_HALF" },
            new { id = "EXTRA_TIME_FIRST_HALF", name = "Extra Time - First Half", type = "Half", order = 4, durationMinutes = 10, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, nextSegmentId = "EXTRA_TIME_BREAK" },
            new { id = "EXTRA_TIME_BREAK", name = "Extra Time Break", type = "Break", order = 5, durationMinutes = 5, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = "EXTRA_TIME_SECOND_HALF" },
            new { id = "EXTRA_TIME_SECOND_HALF", name = "Extra Time - Second Half", type = "Half", order = 6, durationMinutes = 10, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, nextSegmentId = "SUDDEN_DEATH" },
            new { id = "SUDDEN_DEATH", name = "Sudden Death", type = "Half", order = 7, durationMinutes = 10, hasStoppageTime = true, stoppageMinutes = 0, isOptional = true, nextSegmentId = "KICK_OFF" },
            new { id = "KICK_OFF", name = "Kicking Contest", type = "Shootout", order = 8, durationMinutes = 0, hasStoppageTime = false, stoppageMinutes = 0, isOptional = true, nextSegmentId = (string?)null }
        };

        return JsonSerializer.Serialize(segments);
    }

    public static string GetEventDefinitions(string variant = RugbyVariants.Fifteens)
        => RugbyTimelinePackage.GetDefinitionsJson(variant);

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
                id = "TRIES",
                name = "Tries",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = true,
                showInContest = true
            },
            new
            {
                id = "CONVERSIONS",
                name = "Conversions",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PENALTY_KICKS",
                name = "Penalty Kicks",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "DROP_GOALS",
                name = "Drop Goals",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
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
                id = "TERRITORY",
                name = "Territory %",
                type = "Percentage",
                aggregation = "Average",
                displayFormat = "P0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TACKLES",
                name = "Tackles",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "MISSED_TACKLES",
                name = "Missed Tackles",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "TACKLE_SUCCESS",
                name = "Tackle Success %",
                type = "Percentage",
                aggregation = "Average",
                displayFormat = "P1",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "RUCKS",
                name = "Rucks",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "MAULS",
                name = "Mauls",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "SCRUMS",
                name = "Scrums",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "LINE_OUTS",
                name = "Line Outs",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "LINE_OUT_WINS",
                name = "Line Out Wins",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "PENALTIES_CONCEDED",
                name = "Penalties Conceded",
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
                id = "METRES_RUN",
                name = "Metres Run",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "OFFLOADS",
                name = "Offloads",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            },
            new
            {
                id = "CLEAN_BREAKS",
                name = "Clean Breaks",
                type = "Integer",
                aggregation = "Sum",
                displayFormat = "N0",
                showInStandings = false,
                showInContest = true
            }
        };

        return JsonSerializer.Serialize(metrics);
    }

    public static string GetResultMethods(string variant = RugbyVariants.Fifteens)
    {
        variant = RugbyVariants.Normalize(variant);
        var winPoints = variant == RugbyVariants.Sevens ? 3 : 4;
        var drawPoints = 2;
        var lossPoints = variant == RugbyVariants.Sevens ? 1 : 0;
        var results = new[]
        {
            new
            {
                id = "WIN",
                name = "Win",
                type = "WinLossDraw",
                pointsAwarded = winPoints,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "DRAW",
                name = "Draw",
                type = "WinLossDraw",
                pointsAwarded = drawPoints,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "LOSS",
                name = "Loss",
                type = "WinLossDraw",
                pointsAwarded = lossPoints,
                isPrimary = true,
                tiebreaker = false
            },
            new
            {
                id = "WIN_EXTRA_TIME",
                name = "Win in Extra Time",
                type = "WinLossDraw",
                pointsAwarded = winPoints,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "WIN_SUDDEN_DEATH",
                name = "Win in Sudden Death",
                type = "WinLossDraw",
                pointsAwarded = winPoints,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "LOSS_EXTRA_TIME",
                name = "Loss in Extra Time",
                type = "WinLossDraw",
                pointsAwarded = lossPoints,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "LOSS_SUDDEN_DEATH",
                name = "Loss in Sudden Death",
                type = "WinLossDraw",
                pointsAwarded = lossPoints,
                isPrimary = false,
                tiebreaker = true
            },
            new
            {
                id = "WIN_BY_FORFEIT",
                name = "Win by Forfeit",
                type = "WinLossDraw",
                pointsAwarded = winPoints,
                isPrimary = false,
                tiebreaker = false
            },
            new
            {
                id = "LOSS_BY_FORFEIT",
                name = "Loss by Forfeit",
                type = "WinLossDraw",
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
                criteria = "PointsDifference",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 3,
                criteria = "TriesFor",
                direction = "Descending",
                tiebreaker = true
            },
            new
            {
                order = 4,
                criteria = "SharedPosition",
                direction = "None",
                tiebreaker = false
            }
        };

        return JsonSerializer.Serialize(rules);
    }

    public static string GetDisplayConfiguration(string variant = RugbyVariants.Fifteens)
    {
        variant = RugbyVariants.Normalize(variant);
        var config = new
        {
            variant,
            formatName = variant == RugbyVariants.Sevens ? "Rugby Sevens" : "Rugby Union 15s",
            primaryScoreboard = new
            {
                showPoints = true,
                showTries = true,
                showConversions = true,
                showPenaltyKicks = true,
                showDropGoals = true
            },
            timeline = new
            {
                showTries = true,
                showConversions = true,
                showPenaltyKicks = true,
                showDropGoals = true,
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
                showPointsFor = true,
                showPointsAgainst = true,
                showPointsDifference = true,
                showTriesFor = true,
                showTriesAgainst = true,
                showBonusPoints = variant == RugbyVariants.Fifteens,
                showPoints = true
            },
            colors = new
            {
                homeTeam = "#1E88E5",
                awayTeam = "#E53935",
                win = "#4CAF50",
                draw = "#FFC107",
                loss = "#F44336",
                tryScorer = "#4CAF50",
                conversion = "#2196F3"
            }
        };

        return JsonSerializer.Serialize(config);
    }

    public static string GetValidationConfiguration(string variant = RugbyVariants.Fifteens)
    {
        variant = RugbyVariants.Normalize(variant);
        var isSevens = variant == RugbyVariants.Sevens;
        var config = new
        {
            variant,
            formatName = isSevens ? "Rugby Sevens" : "Rugby Union 15s",
            playersPerSide = isSevens ? 7 : 15,
            contest = new
            {
                minParticipants = 2,
                maxParticipants = 2,
                allowTies = true,
                maxExtraTimeMinutes = isSevens ? 5 : 20,
                requiresSuddenDeath = isSevens,
                maxPlayersOnField = isSevens ? 7 : 15,
                minPlayersOnField = 7
            },
            participants = new
            {
                minRosterSize = isSevens ? 7 : 15,
                maxRosterSize = isSevens ? 13 : 23,
                minOnField = isSevens ? 5 : 15,
                maxOnField = isSevens ? 7 : 15,
                maxSubstitutions = isSevens ? 5 : 8,
                allowUnlimitedSubstitutions = false,
                allowBloodBin = true
            },
            events = new
            {
                allowNegativeScore = false,
                allowScoreAfterFinalWhistle = false,
                validateMinuteSequence = true,
                maxEventsPerMinute = 10,
                requireConversionAfterTry = false,
                allowPenaltyAtAnyTime = true,
                allowDropGoalAtAnyTime = true
            },
            timing = new
            {
                autoStoppageTime = false,
                manualStoppageTime = true,
                allowClockStoppage = true,
                allowEarlyStart = false,
                halfDurationMinutes = isSevens ? 7 : 40,
                extraTimeDurationMinutes = isSevens ? 5 : 10,
                halfTimeDurationMinutes = isSevens ? 2 : 10,
                sinBinMinutes = isSevens ? 2 : 10
            },
            scoring = new
            {
                tryPoints = 5,
                conversionPoints = 2,
                penaltyKickPoints = 3,
                dropGoalPoints = 3,
                penaltyTryPoints = 7
            },
            bonusPoints = new
            {
                tryBonus = !isSevens,
                loseBonus = !isSevens,
                homeWinBonus = false
            },
            standings = new
            {
                winPoints = isSevens ? 3 : 4,
                drawPoints = 2,
                lossPoints = isSevens ? 1 : 0,
                useTryBonus = !isSevens,
                useLosingBonus = !isSevens,
                useGrandSlamBonus = !isSevens
            }
        };

        return JsonSerializer.Serialize(config);
    }
}
