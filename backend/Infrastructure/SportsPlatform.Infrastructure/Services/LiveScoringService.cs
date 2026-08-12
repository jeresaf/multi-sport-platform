using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.LiveScoring;
using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Infrastructure.Services;

public class LiveScoringService : ILiveScoringService
{
    private const string CorrectionRequested = "CorrectionRequested";
    private const string CorrectionApproved = "CorrectionApproved";
    private const string CorrectionRejected = "CorrectionRejected";
    private readonly SportsPlatformDbContext _context;

    public LiveScoringService(SportsPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<LiveScoringOperationResult> SubmitEventAsync(
        LiveEventSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var existingScore = await _context.LiveScores
            .FirstOrDefaultAsync(
                item => item.ContestId == submission.ContestId
                    && (item.EventId == submission.EventId
                        || item.IdempotencyKey == submission.IdempotencyKey),
                cancellationToken);
        if (existingScore is not null)
        {
            return new LiveScoringOperationResult(
                true,
                "Score event was already submitted.",
                existingScore,
                IsReplay: true);
        }

        var lastDeviceSequence = await _context.LiveScores
            .Where(item => item.ContestId == submission.ContestId
                && item.DeviceId == submission.DeviceId)
            .MaxAsync(item => (int?)item.Sequence, cancellationToken) ?? 0;
        var sequence = submission.SequenceNumber ?? lastDeviceSequence + 1;
        if (sequence != lastDeviceSequence + 1)
        {
            // Another request with the same idempotency identity may have
            // committed between the initial replay lookup and this sequence read.
            var concurrentReplay = await _context.LiveScores.AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ContestId == submission.ContestId
                        && (item.EventId == submission.EventId
                            || item.IdempotencyKey == submission.IdempotencyKey),
                    cancellationToken);
            if (concurrentReplay is not null)
                return new LiveScoringOperationResult(
                    true,
                    "Score event was already submitted.",
                    concurrentReplay,
                    IsReplay: true);
            return new LiveScoringOperationResult(
                false,
                $"Invalid sequence for device '{submission.DeviceId}'. Expected {lastDeviceSequence + 1}, received {sequence}.",
                FailureKind: LiveScoringFailureKind.InvalidSequence);
        }

        var score = new LiveScore(
            Guid.NewGuid(),
            submission.ContestId,
            submission.EventId,
            submission.ParticipantId,
            submission.MetricName,
            submission.Value,
            submission.SegmentId,
            submission.ClockSeconds,
            submission.TenantId,
            submission.EventType,
            submission.SecondaryParticipantId,
            submission.DeviceTimestamp,
            submission.DeviceId,
            submission.IdempotencyKey,
            submission.RulesetVersionId);
        score.SetDisplayValue(submission.Value.ToString(CultureInfo.InvariantCulture));
        score.SetScorer(submission.ScorerId.ToString(), null);
        score.SetSequence(sequence);

        await _context.LiveScores.AddAsync(score, cancellationToken);
        var session = await GetActiveSessionAsync(submission.ContestId, cancellationToken);
        session?.IncrementUpdates();
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _context.ChangeTracker.Clear();
            var replay = await _context.LiveScores.AsNoTracking().FirstOrDefaultAsync(
                item => item.ContestId == submission.ContestId
                    && (item.EventId == submission.EventId
                        || item.IdempotencyKey == submission.IdempotencyKey),
                cancellationToken);
            if (replay is not null)
                return new LiveScoringOperationResult(
                    true,
                    "Score event was already submitted.",
                    replay,
                    IsReplay: true);
            throw;
        }
        return new LiveScoringOperationResult(true, "Score event submitted.", score);
    }

    public async Task<LiveScoringOperationResult> UpdateEventAsync(
        Guid contestId,
        Guid eventId,
        decimal newValue,
        string? updatedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var score = await _context.LiveScores
            .FirstOrDefaultAsync(
                item => item.ContestId == contestId &&
                    (item.Id == eventId || item.EventId == eventId),
                cancellationToken);
        if (score is null)
            return new LiveScoringOperationResult(false, "Score event not found.");

        var oldValue = score.Value.ToString(CultureInfo.InvariantCulture);
        var newDisplayValue = newValue.ToString(CultureInfo.InvariantCulture);
        score.UpdateValue(newValue, newDisplayValue);

        var update = new LiveScoreUpdate(
            Guid.NewGuid(),
            score.Id,
            reason is null ? "ValueChanged" : $"ValueChanged: {reason}",
            oldValue,
            newDisplayValue,
            updatedBy);

        await _context.LiveScoreUpdates.AddAsync(update, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new LiveScoringOperationResult(true, "Score event updated.", score);
    }

    public async Task<LiveScoringOperationResult> ReverseEventAsync(
        Guid contestId,
        Guid eventId,
        string? reversedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var score = await _context.LiveScores.FirstOrDefaultAsync(
            item => item.ContestId == contestId &&
                (item.Id == eventId || item.EventId == eventId),
            cancellationToken);
        if (score is null)
            return new LiveScoringOperationResult(false, "Score event not found.");

        if (score.Value == 0)
            return new LiveScoringOperationResult(false, "Score event is already reversed.");

        var oldValue = score.Value.ToString(CultureInfo.InvariantCulture);
        score.UpdateValue(0, "0");
        await _context.LiveScoreUpdates.AddAsync(
            new LiveScoreUpdate(
                Guid.NewGuid(),
                score.Id,
                $"Reversed: {reason}",
                oldValue,
                "0",
                reversedBy),
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new LiveScoringOperationResult(true, "Score event reversed.", score);
    }

    public async Task<LiveScoreCorrectionResult> RequestCorrectionAsync(
        Guid contestId,
        Guid eventId,
        LiveScoreCorrectionProposal proposal,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proposal.Reason))
            return new(false, "A correction reason is required.");
        if (proposal.Kind == LiveScoreCorrectionKind.Correct && proposal.Value is null)
            return new(false, "A corrected value is required.");

        var score = await _context.LiveScores.FirstOrDefaultAsync(
            item => item.ContestId == contestId &&
                (item.Id == eventId || item.EventId == eventId),
            cancellationToken);
        if (score is null) return new(false, "Score event not found.");

        var request = new LiveScoreUpdate(
            Guid.NewGuid(),
            score.Id,
            CorrectionRequested,
            JsonSerializer.Serialize(ToSnapshot(score)),
            JsonSerializer.Serialize(proposal with { Reason = proposal.Reason.Trim() }),
            requestedBy.ToString());
        await _context.LiveScoreUpdates.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new(true, "Correction submitted for approval.",
            ToView(request, proposal, "Pending", null), score);
    }

    public async Task<LiveScoreCorrectionResult> ReviewCorrectionAsync(
        Guid contestId,
        Guid correctionId,
        bool approve,
        Guid reviewedBy,
        string? reviewReason,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.LiveScoreUpdates
            .FirstOrDefaultAsync(item => item.Id == correctionId &&
                item.UpdateType == CorrectionRequested, cancellationToken);
        if (request is null) return new(false, "Correction request not found.");

        var score = await _context.LiveScores.FirstOrDefaultAsync(
            item => item.Id == request.LiveScoreId && item.ContestId == contestId,
            cancellationToken);
        if (score is null) return new(false, "Correction request not found.");

        var requestId = correctionId.ToString();
        var resolution = await _context.LiveScoreUpdates.FirstOrDefaultAsync(
            item => item.LiveScoreId == score.Id && item.OldValue == requestId &&
                (item.UpdateType == CorrectionApproved || item.UpdateType == CorrectionRejected),
            cancellationToken);
        if (resolution is not null)
            return new(false, "Correction request has already been reviewed.");

        var proposal = DeserializeProposal(request.NewValue);
        if (proposal is null) return new(false, "Correction request payload is invalid.");

        if (approve)
        {
            switch (proposal.Kind)
            {
                case LiveScoreCorrectionKind.Correct:
                    if (proposal.Value is null) return new(false, "A corrected value is required.");
                    score.UpdateValue(proposal.Value.Value);
                    break;
                case LiveScoreCorrectionKind.Reverse:
                    score.UpdateValue(0, "0");
                    break;
                case LiveScoreCorrectionKind.Replace:
                    score.Replace(
                        proposal.EventType ?? score.EventType,
                        proposal.ParticipantId ?? score.ParticipantId,
                        proposal.SecondaryParticipantId,
                        proposal.SegmentId ?? score.SegmentId,
                        proposal.MetricName ?? score.MetricName,
                        proposal.Value ?? score.Value,
                        proposal.ClockSeconds ?? score.ClockSeconds);
                    break;
            }
        }

        resolution = new LiveScoreUpdate(
            Guid.NewGuid(),
            score.Id,
            approve ? CorrectionApproved : CorrectionRejected,
            requestId,
            string.IsNullOrWhiteSpace(reviewReason) ? null : reviewReason.Trim(),
            reviewedBy.ToString());
        await _context.LiveScoreUpdates.AddAsync(resolution, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new(true, approve ? "Correction approved." : "Correction rejected.",
            ToView(request, proposal, approve ? "Approved" : "Rejected", resolution), score);
    }

    public async Task<IReadOnlyList<LiveScoreCorrectionView>> GetCorrectionsAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var scoreIds = await _context.LiveScores
            .Where(item => item.ContestId == contestId)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var updates = await _context.LiveScoreUpdates
            .Where(item => scoreIds.Contains(item.LiveScoreId) &&
                (item.UpdateType == CorrectionRequested ||
                 item.UpdateType == CorrectionApproved ||
                 item.UpdateType == CorrectionRejected))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        var resolutions = updates
            .Where(item => item.UpdateType != CorrectionRequested && item.OldValue is not null)
            .GroupBy(item => item.OldValue!)
            .ToDictionary(group => group.Key, group => group.First());

        return updates.Where(item => item.UpdateType == CorrectionRequested)
            .Select(request =>
            {
                var proposal = DeserializeProposal(request.NewValue) ??
                    new LiveScoreCorrectionProposal(LiveScoreCorrectionKind.Correct, "Invalid request");
                resolutions.TryGetValue(request.Id.ToString(), out var review);
                var status = review?.UpdateType == CorrectionApproved ? "Approved"
                    : review?.UpdateType == CorrectionRejected ? "Rejected" : "Pending";
                return ToView(request, proposal, status, review);
            })
            .ToArray();
    }

    private static LiveScoreCorrectionProposal? DeserializeProposal(string? value)
    {
        try { return value is null ? null : JsonSerializer.Deserialize<LiveScoreCorrectionProposal>(value); }
        catch (JsonException) { return null; }
    }

    private static object ToSnapshot(LiveScore score) => new
    {
        score.EventType,
        score.ParticipantId,
        score.SecondaryParticipantId,
        score.SegmentId,
        score.MetricName,
        score.Value,
        score.ClockSeconds
    };

    private static LiveScoreCorrectionView ToView(
        LiveScoreUpdate request,
        LiveScoreCorrectionProposal proposal,
        string status,
        LiveScoreUpdate? review) => new(
            request.Id,
            request.LiveScoreId,
            proposal.Kind,
            proposal.Reason,
            status,
            request.UpdatedBy,
            request.UpdatedAt,
            review?.UpdatedBy,
            review?.UpdatedAt,
            review?.NewValue,
            request.OldValue,
            request.NewValue);

    public async Task<IReadOnlyList<LiveScore>> GetContestScoresAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.LiveScores
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LiveScoreUpdate>> GetScoreHistoryAsync(
        Guid contestId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var scoreIds = _context.LiveScores
            .Where(item => item.ContestId == contestId)
            .Select(item => item.Id);

        return await _context.LiveScoreUpdates
            .Where(item => scoreIds.Contains(item.LiveScoreId))
            .OrderByDescending(item => item.UpdatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<LiveScoreSession?> GetActiveSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.LiveScoreSessions
            .Where(item => item.ContestId == contestId &&
                           (item.Status == SessionStatus.Active || item.Status == SessionStatus.Paused))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LiveScoreSession> StartSessionAsync(
        Guid contestId,
        Guid eventId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (await GetActiveSessionAsync(contestId, cancellationToken) is not null)
            throw new InvalidOperationException("An active scoring session already exists for this contest.");

        var session = new LiveScoreSession(Guid.NewGuid(), contestId, eventId, name);
        session.Start();
        await _context.LiveScoreSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public Task<LiveScoringOperationResult> PauseSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        ChangeSessionAsync(contestId, SessionStatus.Active, "paused", session => session.Pause(), cancellationToken);

    public Task<LiveScoringOperationResult> ResumeSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        ChangeSessionAsync(contestId, SessionStatus.Paused, "resumed", session => session.Resume(), cancellationToken);

    public Task<LiveScoringOperationResult> EndSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        ChangeSessionAsync(contestId, null, "ended", session => session.End(), cancellationToken);

    public async Task<LiveScoringOperationResult> SetSessionContextAsync(
        Guid contestId,
        Guid segmentId,
        decimal clockSeconds,
        bool clockCountsDown,
        CancellationToken cancellationToken = default)
    {
        var session = await GetActiveSessionAsync(contestId, cancellationToken);
        if (session is null)
            return new(false, "No active scoring session found.");
        session.SetContext(segmentId, clockSeconds, clockCountsDown);
        await _context.SaveChangesAsync(cancellationToken);
        return new(true, "Scoring context updated.");
    }

    public Task<LiveScoringOperationResult> StartClockAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        ChangeSessionAsync(
            contestId,
            SessionStatus.Active,
            "clock started",
            session => session.StartClock(),
            cancellationToken);

    public Task<LiveScoringOperationResult> PauseClockAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        ChangeSessionAsync(
            contestId,
            SessionStatus.Active,
            "clock paused",
            session => session.PauseClock(),
            cancellationToken);

    private async Task<LiveScoringOperationResult> ChangeSessionAsync(
        Guid contestId,
        SessionStatus? requiredStatus,
        string action,
        Action<LiveScoreSession> change,
        CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(contestId, cancellationToken);
        if (session is null || requiredStatus.HasValue && session.Status != requiredStatus.Value)
            return new LiveScoringOperationResult(false, $"No scoring session is available to be {action}.");

        change(session);
        await _context.SaveChangesAsync(cancellationToken);
        return new LiveScoringOperationResult(true, $"Scoring session {action}.");
    }
}
