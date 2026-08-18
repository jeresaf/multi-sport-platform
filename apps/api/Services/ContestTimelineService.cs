using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Sports.Services;

namespace SportsPlatform.Api.Services;

public sealed class ContestTimelineService(SportsPlatformDbContext database)
{
    public async Task<ContestTimelineResponse?> GetAsync(
        Guid contestId,
        Guid? tenantId,
        string mode,
        long? afterSequence,
        long? afterVersion,
        string? period,
        int limit,
        CancellationToken cancellationToken)
    {
        var contest = await database.Contests.AsNoTracking()
            .Include(item => item.Events)
            .Include(item => item.Segments)
            .Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == contestId
                && (!tenantId.HasValue || item.TenantId == tenantId.Value), cancellationToken);
        if (contest is null) return null;

        var sportCode = await database.Sports.AsNoTracking()
            .Where(item => item.Id == contest.SportId)
            .Select(item => item.Code)
            .SingleOrDefaultAsync(cancellationToken) ?? "UNKNOWN";
        var liveScores = await database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .ToArrayAsync(cancellationToken);
        var scoreIds = liveScores.Select(item => item.Id).ToArray();
        var updates = scoreIds.Length == 0
            ? []
            : await database.LiveScoreUpdates.AsNoTracking()
                .Where(item => scoreIds.Contains(item.LiveScoreId))
                .OrderBy(item => item.UpdatedAt)
                .ToArrayAsync(cancellationToken);
        var updatesByScore = updates
            .GroupBy(item => item.LiveScoreId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var segmentNames = contest.Segments.ToDictionary(item => item.Id, item => item.Name);
        var actorIds = contest.Participants.Select(item => item.ParticipantId)
            .Concat(liveScores.Select(item => item.SecondaryParticipantId ?? Guid.Empty))
            .Concat(liveScores.Select(item => item.PrimaryPlayerId ?? Guid.Empty))
            .Concat(liveScores.Select(item => item.SecondaryPlayerId ?? Guid.Empty))
            .Concat(contest.Events.Select(item => item.SecondaryParticipantId ?? Guid.Empty))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var actorNames = await database.Participants.AsNoTracking()
            .Where(item => actorIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var projected = new List<TimelineProjection>();
        projected.AddRange(liveScores.Select(score =>
        {
            var eventUpdates = updatesByScore.GetValueOrDefault(score.Id) ?? [];
            var status = StatusFor(eventUpdates, score.Value);
            return new TimelineProjection(
                score.EventId == Guid.Empty ? score.Id : score.EventId,
                CanonicalType(sportCode, score.EventType),
                CategoryFor(sportCode, score.EventType, score.MetricName),
                ImportanceFor(sportCode, score.EventType, score.MetricName),
                status,
                score.ParticipantId,
                score.SecondaryParticipantId,
                score.PrimaryPlayerId ?? score.SecondaryParticipantId,
                score.SecondaryPlayerId,
                score.SegmentId,
                score.SegmentId.HasValue
                    ? segmentNames.GetValueOrDefault(score.SegmentId.Value)
                    : null,
                score.ClockSeconds,
                score.Value,
                score.DisplayValue,
                score.MetricName,
                score.TimelineSequence,
                score.DeviceTimestamp,
                score.ServerTimestamp,
                "SCORER",
                score.Metadata);
        }));

        var liveEventIds = projected.Select(item => item.Id).ToHashSet();
        projected.AddRange(contest.Events
            .Where(item => !liveEventIds.Contains(item.Id))
            .Select(item => new TimelineProjection(
                item.Id,
                CanonicalType(sportCode, item.EventType),
                CategoryFor(sportCode, item.EventType, null),
                ImportanceFor(sportCode, item.EventType, null),
                StatusFor(item.Status),
                item.ParticipantId,
                item.SecondaryParticipantId,
                item.SecondaryParticipantId,
                null,
                item.SegmentId,
                item.SegmentId.HasValue
                    ? segmentNames.GetValueOrDefault(item.SegmentId.Value)
                    : null,
                null,
                Decimal(item.MetricValue),
                item.MetricValue,
                null,
                item.TimelineSequence,
                item.DeviceTimestamp,
                item.ServerTimestamp,
                "CONTEST",
                null)));

        var ordered = projected
            .OrderBy(item => item.SequenceHint <= 0 ? long.MaxValue : item.SequenceHint)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.Id)
            .Select((item, index) => item with
            {
                Sequence = item.SequenceHint > 0 ? item.SequenceHint : index + 1L
            })
            .ToArray();
        var sides = contest.Participants.OrderBy(item => item.SortOrder)
            .Select(item => item.ParticipantId)
            .Take(2)
            .ToArray();
        decimal homeScore = 0;
        decimal awayScore = 0;
        decimal shootoutHomeScore = 0;
        decimal shootoutAwayScore = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (item.Status is not ("REVERSED" or "VOID") && item.Value.HasValue)
            {
                var football = Normalize(sportCode) == "FOOTBALL"
                    ? FootballTimelinePackage.Find(item.EventType)
                    : null;
                var effect = football?.ScoreEffect
                    ?? (item.MetricName?.Equals("score", StringComparison.OrdinalIgnoreCase) == true
                        ? "REGULATION"
                        : item.MetricName?.Equals("shootout_score", StringComparison.OrdinalIgnoreCase) == true
                            ? "SHOOTOUT"
                            : "NONE");
                if (effect == "REGULATION")
                {
                    if (sides.ElementAtOrDefault(0) == item.PrimaryParticipantId) homeScore += item.Value.Value;
                    if (sides.ElementAtOrDefault(1) == item.PrimaryParticipantId) awayScore += item.Value.Value;
                }
                if (effect == "SHOOTOUT")
                {
                    if (sides.ElementAtOrDefault(0) == item.PrimaryParticipantId) shootoutHomeScore += item.Value.Value;
                    if (sides.ElementAtOrDefault(1) == item.PrimaryParticipantId) shootoutAwayScore += item.Value.Value;
                }
            }
            ordered[index] = item with
            {
                HomeScoreAfter = homeScore,
                AwayScoreAfter = awayScore,
                ShootoutHomeScoreAfter = shootoutHomeScore,
                ShootoutAwayScoreAfter = shootoutAwayScore
            };
        }
        var currentVersion = Math.Max(contest.TimelineVersion, ordered.LongLength);
        if (afterVersion > currentVersion)
            return new ContestTimelineResponse(
                contestId, currentVersion, CoverageFor(sportCode), [], null, true);
        if (afterVersion == currentVersion)
            return new ContestTimelineResponse(
                contestId, currentVersion, CoverageFor(sportCode), [], null, false);

        IEnumerable<TimelineProjection> filtered = ordered;
        if (afterSequence.HasValue)
            filtered = filtered.Where(item => item.Sequence > afterSequence.Value);
        if (!string.IsNullOrWhiteSpace(period))
            filtered = filtered.Where(item => string.Equals(
                Normalize(item.Period), Normalize(period), StringComparison.Ordinal));
        if (string.Equals(mode, "key", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(item => item.Importance == "KEY");

        var page = filtered.Take(limit + 1).ToArray();
        var hasMore = page.Length > limit;
        var items = page.Take(limit).Select(item => new ContestTimelineEvent(
            item.Id,
            item.Sequence,
            item.Category,
            Normalize(item.EventType),
            sportCode.ToUpperInvariant(),
            SportVariant(sportCode, contest.ContestShape),
            item.Period,
            item.ClockSeconds.HasValue ? Clock(item.ClockSeconds.Value) : null,
            item.PrimaryParticipantId == Guid.Empty ? null : item.PrimaryParticipantId,
            item.PrimaryParticipantId == Guid.Empty
                ? null
                : actorNames.GetValueOrDefault(item.PrimaryParticipantId),
            item.SecondaryParticipantId,
            item.SecondaryParticipantId.HasValue
                ? actorNames.GetValueOrDefault(item.SecondaryParticipantId.Value)
                : null,
            item.PrimaryPlayerId,
            item.PrimaryPlayerId.HasValue
                ? actorNames.GetValueOrDefault(item.PrimaryPlayerId.Value)
                : null,
            item.SecondaryPlayerId,
            item.SecondaryPlayerId.HasValue
                ? actorNames.GetValueOrDefault(item.SecondaryPlayerId.Value)
                : null,
            item.Value,
            item.DisplayValue,
            item.MetricName,
            item.Status,
            item.Importance,
            item.HomeScoreAfter,
            item.AwayScoreAfter,
            item.ShootoutHomeScoreAfter,
            item.ShootoutAwayScoreAfter,
            item.OccurredAt,
            item.RecordedAt,
            item.Source,
            item.MetadataJson)).ToArray();

        return new ContestTimelineResponse(
            contestId,
            currentVersion,
            CoverageFor(sportCode),
            items,
            hasMore && items.Length > 0 ? items[^1].Sequence : null,
            false);
    }

    private static string StatusFor(IEnumerable<LiveScoreUpdate> updates, decimal value)
    {
        var types = updates.Select(item => item.UpdateType).ToArray();
        if (types.Any(item => item.StartsWith("Reversed:", StringComparison.OrdinalIgnoreCase))
            && value == 0) return "REVERSED";
        if (types.Any(item => item == "CorrectionApproved"
            || item.StartsWith("ValueChanged", StringComparison.OrdinalIgnoreCase)))
            return "CORRECTED";
        if (types.Any(item => item == "CorrectionRequested")) return "PENDING_REVIEW";
        return "CONFIRMED";
    }

    private static string StatusFor(EventStatus status) => status switch
    {
        EventStatus.Pending => "PENDING_REVIEW",
        EventStatus.Approved => "CONFIRMED",
        EventStatus.Rejected => "VOID",
        EventStatus.Superseded => "CORRECTED",
        _ => "ACTIVE"
    };

    private static string CategoryFor(string sportCode, string eventType, string? metricName)
    {
        if (Normalize(sportCode) == "FOOTBALL"
            && FootballTimelinePackage.Find(eventType) is { } football)
            return football.Category;
        var value = Normalize($"{eventType}_{metricName}");
        if (Has(value, "PERIOD", "HALF", "QUARTER", "INNINGS", "OVER_START", "OVER_END")) return "PERIOD";
        if (Has(value, "CARD", "FOUL", "SIN_BIN", "EJECTION", "DISCIPLINE")) return "DISCIPLINE";
        if (Has(value, "SUBSTITUTION", "LINEUP", "PERSONNEL")) return "PERSONNEL";
        if (Has(value, "VAR", "REVIEW", "CHALLENGE")) return "REVIEW";
        if (Has(value, "RAIN", "DELAY", "TIMEOUT", "STOPPAGE", "SUSPEND")) return "STOPPAGE";
        if (Has(value, "GOAL", "POINT", "TRY", "CONVERSION", "DROP_GOAL", "FIELD_GOAL",
                "FREE_THROW", "RUN", "BOUNDARY", "SIX", "FOUR", "WICKET", "SCORE")) return "SCORING";
        if (Has(value, "COMMENTARY", "NOTE")) return "COMMENTARY";
        return "STATISTIC";
    }

    private static string ImportanceFor(string sportCode, string eventType, string? metricName)
    {
        if (Normalize(sportCode) == "FOOTBALL"
            && FootballTimelinePackage.Find(eventType) is { } football)
            return football.Importance;
        var category = CategoryFor(sportCode, eventType, metricName);
        return category is "SCORING" or "DISCIPLINE" or "REVIEW" or "STOPPAGE" ? "KEY" : "STANDARD";
    }

    private static string CanonicalType(string sportCode, string eventType)
    {
        if (Normalize(sportCode) != "FOOTBALL") return eventType;
        return Normalize(eventType) switch
        {
            "SECOND_YELLOW" => "SECOND_YELLOW_CARD",
            "SUBSTITUTION_IN" => "PLAYER_IN",
            "SUBSTITUTION_OUT" => "PLAYER_OUT",
            "PENALTY_SCORED" => "PENALTY_SHOOTOUT_SCORE",
            "PENALTY_SAVED" => "PENALTY_SHOOTOUT_MISS",
            _ => eventType
        };
    }

    private static bool Has(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    private static decimal? Decimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join('_', value.Trim().ToUpperInvariant()
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries));

    private static string Clock(decimal seconds)
    {
        var total = Math.Max(0, (int)Math.Floor(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static string? SportVariant(string sportCode, string contestShape)
    {
        var value = Normalize($"{sportCode}_{contestShape}");
        if (!value.Contains("RUGBY", StringComparison.Ordinal)) return null;
        return value.Contains("SEVEN", StringComparison.Ordinal) || value.Contains("7", StringComparison.Ordinal)
            ? "RUGBY_7S"
            : "RUGBY_15S";
    }

    private static TimelineCoverage CoverageFor(string sportCode) => new(
        KeyEvents: true,
        AllEvents: true,
        PlayByPlay: Normalize(sportCode) is "FOOTBALL" or "BASKETBALL" or "RUGBY" or "RUGBY_7S" or "RUGBY_15S" or "CRICKET",
        PlayerAttribution: true,
        Coordinates: false,
        Lineups: false);

    private sealed record TimelineProjection(
        Guid Id,
        string EventType,
        string Category,
        string Importance,
        string Status,
        Guid PrimaryParticipantId,
        Guid? SecondaryParticipantId,
        Guid? PrimaryPlayerId,
        Guid? SecondaryPlayerId,
        Guid? SegmentId,
        string? Period,
        decimal? ClockSeconds,
        decimal? Value,
        string? DisplayValue,
        string? MetricName,
        long SequenceHint,
        DateTime OccurredAt,
        DateTime RecordedAt,
        string Source,
        string? MetadataJson)
    {
        public long Sequence { get; init; }
        public decimal HomeScoreAfter { get; init; }
        public decimal AwayScoreAfter { get; init; }
        public decimal ShootoutHomeScoreAfter { get; init; }
        public decimal ShootoutAwayScoreAfter { get; init; }
    }
}

public sealed record ContestTimelineResponse(
    Guid ContestId,
    long TimelineVersion,
    TimelineCoverage Coverage,
    IReadOnlyList<ContestTimelineEvent> Events,
    long? NextSequence,
    bool VersionConflict);

public sealed record TimelineCoverage(
    bool KeyEvents,
    bool AllEvents,
    bool PlayByPlay,
    bool PlayerAttribution,
    bool Coordinates,
    bool Lineups);

public sealed record ContestTimelineEvent(
    Guid Id,
    long Sequence,
    string Category,
    string Type,
    string SportCode,
    string? SportVariant,
    string? Period,
    string? Clock,
    Guid? PrimaryParticipantId,
    string? PrimaryParticipantName,
    Guid? SecondaryParticipantId,
    string? SecondaryParticipantName,
    Guid? PrimaryPlayerId,
    string? PrimaryPlayerName,
    Guid? SecondaryPlayerId,
    string? SecondaryPlayerName,
    decimal? Value,
    string? DisplayValue,
    string? MetricName,
    string Status,
    string Importance,
    decimal HomeScoreAfter,
    decimal AwayScoreAfter,
    decimal ShootoutHomeScoreAfter,
    decimal ShootoutAwayScoreAfter,
    DateTime OccurredAt,
    DateTime RecordedAt,
    string Source,
    string? MetadataJson);
