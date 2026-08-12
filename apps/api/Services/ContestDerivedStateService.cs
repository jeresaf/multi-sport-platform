using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Api.Services;

public sealed class ContestDerivedStateService(SportsPlatformDbContext database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ContestSnapshotEnvelope?> GetLatestAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var json = await database.AuditEntries.AsNoTracking()
            .Where(item => item.Action == "ContestSnapshotRebuilt"
                && item.EntityType == "Contest"
                && item.EntityId == contestId.ToString())
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Id)
            .Select(item => item.NewValue)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ContestSnapshotEnvelope>(json, JsonOptions);
    }

    public async Task<ContestSnapshotEnvelope?> RebuildAsync(
        Guid contestId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var contest = await database.Contests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == contestId, cancellationToken);
        if (contest is null) return null;

        var events = await database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.ServerTimestamp)
            .ThenBy(item => item.Id)
            .Select(item => new SnapshotEvent(
                item.Id,
                item.EventId,
                item.ParticipantId,
                item.SegmentId,
                item.EventType,
                item.MetricName,
                item.Value,
                item.DisplayValue,
                item.ClockSeconds,
                item.Sequence))
            .ToArrayAsync(cancellationToken);
        var results = await database.ContestResults.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.ParticipantId)
            .Select(item => new SnapshotResult(
                item.Id,
                item.ParticipantId,
                item.Status,
                item.Rank,
                item.Score,
                item.ScoreDisplay,
                item.Time))
            .ToArrayAsync(cancellationToken);
        var session = await database.LiveScoreSessions.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new SnapshotClock(
                item.Id,
                item.Status.ToString(),
                item.ActiveSegmentId,
                item.ClockSeconds,
                item.ClockCountsDown,
                item.IsClockRunning,
                item.ClockStartedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var competitionId = await ResolveCompetitionIdAsync(contest.StageId, cancellationToken);
        var standings = contest.GroupId.HasValue
            ? await database.Standings.AsNoTracking()
                .Where(item => item.GroupId == contest.GroupId.Value)
                .OrderBy(item => item.Position)
                .ThenBy(item => item.ParticipantId)
                .Select(item => new SnapshotStanding(
                    item.ParticipantId,
                    item.Position,
                    item.Played,
                    item.Won,
                    item.Drawn,
                    item.Lost,
                    item.PointsFor,
                    item.PointsAgainst,
                    item.Points))
                .ToArrayAsync(cancellationToken)
            : [];
        var leaderboards = await database.Leaderboards.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.Id)
            .Select(item => new SnapshotLeaderboard(
                item.Id,
                item.Name,
                item.Type.ToString(),
                item.IsActive,
                database.LeaderboardEntries.Count(entry => entry.LeaderboardId == item.Id)))
            .ToArrayAsync(cancellationToken);
        var brackets = await (
                from match in database.BracketMatches.AsNoTracking()
                join round in database.BracketRounds.AsNoTracking() on match.RoundId equals round.Id
                join bracket in database.Brackets.AsNoTracking() on round.BracketId equals bracket.Id
                where match.ContestId == contestId || (contest.SeriesId.HasValue
                    && match.SeriesId == contest.SeriesId)
                orderby bracket.Id, round.RoundNumber, match.MatchNumber
                select new SnapshotBracketImpact(
                    bracket.Id,
                    bracket.Status.ToString(),
                    bracket.ChampionId,
                    round.RoundNumber,
                    match.Id,
                    match.Status.ToString(),
                    match.WinnerId,
                    match.NextMatchId))
            .ToArrayAsync(cancellationToken);

        var currentScores = events
            .GroupBy(item => new { item.ParticipantId, item.MetricName })
            .Select(group => new SnapshotScore(
                group.Key.ParticipantId,
                group.Key.MetricName,
                group.Sum(item => item.Value)))
            .OrderBy(item => item.ParticipantId)
            .ThenBy(item => item.MetricName)
            .ToArray();
        var segmentScores = events
            .GroupBy(item => new { item.SegmentId, item.ParticipantId, item.MetricName })
            .Select(group => new SnapshotSegmentScore(
                group.Key.SegmentId,
                group.Key.ParticipantId,
                group.Key.MetricName,
                group.Sum(item => item.Value)))
            .OrderBy(item => item.SegmentId)
            .ThenBy(item => item.ParticipantId)
            .ThenBy(item => item.MetricName)
            .ToArray();

        var state = new ContestDerivedState(
            contest.Id,
            contest.TenantId,
            contest.Status.ToString(),
            contest.RulesetVersionId,
            contest.WinnerId ?? results.FirstOrDefault(item => item.Rank == 1
                && item.Status is ContestResultStatus.Approved
                    or ContestResultStatus.Published
                    or ContestResultStatus.Official)?.ParticipantId,
            contest.ResultMethod,
            events,
            currentScores,
            segmentScores,
            session,
            results,
            new QualificationImpact(
                contest.StageId,
                contest.GroupId,
                contest.QualificationAppliedAt,
                competitionId),
            standings,
            brackets,
            leaderboards);
        var sourceHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, JsonOptions))));
        var version = await database.AuditEntries.CountAsync(item =>
            item.Action == "ContestSnapshotRebuilt"
            && item.EntityType == "Contest"
            && item.EntityId == contestId.ToString(), cancellationToken) + 1;
        var envelope = new ContestSnapshotEnvelope(
            version,
            sourceHash,
            DateTime.UtcNow,
            actorId,
            reason.Trim(),
            state);
        database.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(),
            contest.TenantId,
            actorId,
            "ContestSnapshotRebuilt",
            "Contest",
            contestId.ToString(),
            null,
            JsonSerializer.Serialize(envelope, JsonOptions),
            reason.Trim()));
        await database.SaveChangesAsync(cancellationToken);
        return envelope;
    }

    private async Task<Guid?> ResolveCompetitionIdAsync(
        Guid? stageId,
        CancellationToken cancellationToken)
    {
        if (!stageId.HasValue) return null;
        return await database.Competitions.AsNoTracking()
            .Where(competition => competition.Editions.Any(edition =>
                edition.Stages.Any(stage => stage.Id == stageId.Value)))
            .Select(competition => (Guid?)competition.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record ContestSnapshotEnvelope(
    int Version,
    string SourceHash,
    DateTime RebuiltAt,
    Guid RebuiltBy,
    string Reason,
    ContestDerivedState State);

public sealed record ContestDerivedState(
    Guid ContestId,
    Guid TenantId,
    string Status,
    Guid? RulesetVersionId,
    Guid? WinnerId,
    string? ResultMethod,
    IReadOnlyList<SnapshotEvent> Events,
    IReadOnlyList<SnapshotScore> CurrentScores,
    IReadOnlyList<SnapshotSegmentScore> SegmentScores,
    SnapshotClock? Clock,
    IReadOnlyList<SnapshotResult> Results,
    QualificationImpact Qualification,
    IReadOnlyList<SnapshotStanding> Standings,
    IReadOnlyList<SnapshotBracketImpact> Brackets,
    IReadOnlyList<SnapshotLeaderboard> Leaderboards);

public sealed record SnapshotEvent(Guid Id, Guid EventId, Guid ParticipantId, Guid? SegmentId,
    string EventType, string MetricName, decimal Value, string? DisplayValue,
    decimal? ClockSeconds, int Sequence);
public sealed record SnapshotScore(Guid ParticipantId, string MetricName, decimal Value);
public sealed record SnapshotSegmentScore(Guid? SegmentId, Guid ParticipantId,
    string MetricName, decimal Value);
public sealed record SnapshotClock(Guid SessionId, string Status, Guid? ActiveSegmentId,
    decimal StoredSeconds, bool CountsDown, bool IsRunning, DateTime? StartedAt);
public sealed record SnapshotResult(Guid Id, Guid ParticipantId, ContestResultStatus Status,
    int? Rank, decimal? Score, string? ScoreDisplay, string? Time);
public sealed record QualificationImpact(Guid? StageId, Guid? GroupId,
    DateTime? AppliedAt, Guid? CompetitionId);
public sealed record SnapshotStanding(Guid ParticipantId, int Position, int Played, int Won,
    int Drawn, int Lost, int PointsFor, int PointsAgainst, decimal Points);
public sealed record SnapshotBracketImpact(Guid BracketId, string BracketStatus,
    Guid? ChampionId, int RoundNumber, Guid MatchId, string MatchStatus,
    Guid? WinnerId, Guid? NextMatchId);
public sealed record SnapshotLeaderboard(Guid Id, string Name, string Type,
    bool IsActive, int EntryCount);
