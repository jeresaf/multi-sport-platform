using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Api.Hubs;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;

namespace SportsPlatform.Api.Controllers;

/// <summary>
/// Controller for live scoring operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission("scoring.manage")]
public class LiveScoringController : ControllerBase
{
    private readonly ILiveScoringService _liveScoringService;
    private readonly IContestsService _contestsService;
    private readonly ContestHubNotifier _notifier;
    private readonly SportsPlatformDbContext _database;
    private readonly ILogger<LiveScoringController> _logger;
    private readonly ContestDerivedStateService _derivedState;

    public LiveScoringController(
        ILiveScoringService liveScoringService,
        IContestsService contestsService,
        ContestHubNotifier notifier,
        SportsPlatformDbContext database,
        ContestDerivedStateService derivedState,
        ILogger<LiveScoringController> logger)
    {
        _liveScoringService = liveScoringService;
        _contestsService = contestsService;
        _notifier = notifier;
        _database = database;
        _derivedState = derivedState;
        _logger = logger;
    }

    /// <summary>
    /// Gets the sport-configured scoring controls and the contest's segments.
    /// </summary>
    [HttpGet("contests/{contestId}/schema")]
    public async Task<IActionResult> GetScoringSchema(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        var schema = SchemaFor(contest);
        var ruleset = await ResolveEventDefinitionsAsync(
            contest, null, false, HttpContext.RequestAborted);
        if (ruleset.Error is not null)
            return BadRequest(new { Message = ruleset.Error });
        schema = schema is null ? null : WithEventDefinitions(schema, ruleset.Definitions);
        if (schema is not null && contest.RulesetVersionId.HasValue)
        {
            var validationConfiguration = await _database.Sports.AsNoTracking()
                .SelectMany(item => item.Versions)
                .Where(item => item.Id == contest.RulesetVersionId.Value)
                .Select(item => item.ValidationConfiguration)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);
            var formatName = GetFormatName(validationConfiguration);
            if (!string.IsNullOrWhiteSpace(formatName))
                schema = schema with { Name = formatName };
        }
        return schema is null
            ? NotFound(new { Message = "No scoring schema is configured for this sport." })
            : Ok(schema);
    }

    /// <summary>
    /// Gets active roster players for each team in a contest so an event can be
    /// attributed to a player while its primary score remains on the team.
    /// </summary>
    [HttpGet("contests/{contestId}/rosters")]
    public async Task<IActionResult> GetContestRosters(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        var teamIds = contest.Participants
            .Where(item => string.Equals(item.ParticipantType, "Team", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var rosters = await _database.Set<TeamRoster>()
            .AsNoTracking()
            .Include(item => item.Entries)
            .Where(item => item.TenantId == contest.TenantId
                && item.IsActive
                && teamIds.Contains(item.TeamId))
            .ToListAsync(HttpContext.RequestAborted);
        var athleteIds = rosters
            .SelectMany(item => item.Entries)
            .Where(item => item.IsActive)
            .Select(item => item.AthleteId)
            .Distinct()
            .ToArray();
        var athletes = await _database.Set<Athlete>()
            .AsNoTracking()
            .Where(item => athleteIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, HttpContext.RequestAborted);

        return Ok(teamIds.Select(teamId => new
        {
            TeamId = teamId,
            Players = rosters
                .Where(roster => roster.TeamId == teamId)
                .SelectMany(roster => roster.Entries)
                .Where(entry => entry.IsActive && athletes.ContainsKey(entry.AthleteId))
                .GroupBy(entry => entry.AthleteId)
                .Select(group =>
                {
                    var entry = group.First();
                    var athlete = athletes[group.Key];
                    return new
                    {
                        AthleteId = athlete.Id,
                        athlete.Name,
                        athlete.ShortName,
                        entry.JerseyNumber,
                        entry.Position
                    };
                })
                .OrderBy(player => player.JerseyNumber)
                .ThenBy(player => player.Name)
        }));
    }

    /// <summary>
    /// Submits a live score event.
    /// </summary>
    [HttpPost("contests/{contestId}/events")]
    public async Task<IActionResult> SubmitEvent(
        Guid contestId,
        [FromBody] SubmitEventRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var contest = await _contestsService.GetContestByIdAsync(contestId);
            if (contest is null) return NotFound();
            var actorId = User.UserId();
            if (!actorId.HasValue) return Unauthorized();
            var deviceId = string.IsNullOrWhiteSpace(request.DeviceId)
                ? Request.Headers["X-Device-Id"].FirstOrDefault()
                : request.DeviceId;
            deviceId = string.IsNullOrWhiteSpace(deviceId)
                ? $"user:{actorId.Value:N}"
                : deviceId.Trim();
            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.EventId.ToString("N")
                : request.IdempotencyKey.Trim();
            var existing = await _database.LiveScores.AsNoTracking().FirstOrDefaultAsync(item =>
                item.ContestId == contestId
                && (item.EventId == request.EventId
                    || item.IdempotencyKey == idempotencyKey));
            if (existing is not null)
                return Ok(new
                {
                    EventId = existing.EventId,
                    InternalEventId = existing.Id,
                    Event = existing,
                    IsReplay = true,
                    Message = "Score event was already submitted."
                });
            var schema = SchemaFor(contest);
            var session = await _liveScoringService.GetActiveSessionAsync(contestId);
            var defaultSegmentId = FlattenSegments(contest)
                .Select(item => item.Segment)
                .Where(item => item.NestedSegments.Count == 0 && !item.IsOptional)
                .OrderBy(item => item.SortOrder)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault();
            var effectiveRequest = request with
            {
                SegmentId = request.SegmentId ?? session?.ActiveSegmentId ?? defaultSegmentId,
                ClockSeconds = request.ClockSeconds ??
                    (schema?.SupportsClock == true ? session?.CurrentClockSeconds : null),
                DeviceTimestamp = request.DeviceTimestamp ?? DateTime.UtcNow
            };
            var eventType = string.IsNullOrWhiteSpace(request.EventType)
                ? request.MetricName
                : request.EventType.Trim();
            var ruleset = await ResolveEventDefinitionsAsync(
                contest!, request.EventType, !string.IsNullOrWhiteSpace(request.EventType),
                HttpContext.RequestAborted);
            if (ruleset.Error is not null)
                return BadRequest(new { Message = ruleset.Error });
            var validationError = ruleset.Definition is null
                ? ValidateEvent(schema, effectiveRequest, contest)
                : ValidateConfiguredEvent(ruleset.Definition, effectiveRequest, contest);
            if (validationError is not null)
            {
                return BadRequest(new { Message = validationError });
            }
            var secondaryParticipantError = await ValidateSecondaryParticipantAsync(
                contest!, effectiveRequest.ParticipantId,
                effectiveRequest.SecondaryParticipantId,
                effectiveRequest.PrimaryPlayerId,
                effectiveRequest.SecondaryPlayerId);
            if (secondaryParticipantError is not null)
                return BadRequest(new { Message = secondaryParticipantError });
            if (request.DeviceTimestamp.HasValue
                && request.DeviceTimestamp.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
                return BadRequest(new { Message = "Device timestamp cannot be more than five minutes in the future." });
            var result = await _liveScoringService.SubmitEventAsync(
                new LiveEventSubmission(
                    contest!.TenantId,
                    contestId,
                    effectiveRequest.EventId,
                    eventType,
                    effectiveRequest.ParticipantId,
                    effectiveRequest.SecondaryParticipantId,
                    effectiveRequest.SegmentId,
                    effectiveRequest.MetricName,
                    effectiveRequest.Value,
                    effectiveRequest.ClockSeconds,
                    effectiveRequest.DeviceTimestamp?.ToUniversalTime() ?? DateTime.UtcNow,
                    actorId.Value,
                    deviceId,
                    effectiveRequest.SequenceNumber,
                    idempotencyKey,
                    contest.RulesetVersionId,
                    effectiveRequest.PrimaryPlayerId,
                    effectiveRequest.SecondaryPlayerId));

            if (!result.Success)
            {
                if (result.FailureKind == LiveScoringFailureKind.InvalidSequence)
                {
                    var conflict = new AuditEntry(
                        Guid.NewGuid(),
                        contest.TenantId,
                        actorId,
                        "LiveScoreSyncConflict",
                        "Contest",
                        contestId.ToString(),
                        null,
                        JsonSerializer.Serialize(new
                        {
                            request.EventId,
                            request.SequenceNumber,
                            deviceId,
                            deviceTimestamp = request.DeviceTimestamp?.ToUniversalTime()
                        }),
                        result.Message);
                    conflict.SetRequestContext(
                        HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Request.Headers.UserAgent.ToString());
                    _database.AuditEntries.Add(conflict);
                    await _database.SaveChangesAsync(HttpContext.RequestAborted);
                    return Conflict(new { result.Message });
                }
                return BadRequest(new { result.Message });
            }

            // Broadcast the update via SignalR
            var update = new LiveScoreUpdateDto(
                contestId,
                result.Event!.Id,
                effectiveRequest.ParticipantId,
                effectiveRequest.MetricName,
                effectiveRequest.Value,
                result.Event.DisplayValue,
                "Created",
                result.Event.CreatedAt);

            await _notifier.BroadcastLiveScoreUpdate(contestId, tenantId, update);

            return Ok(new
            {
                EventId = result.Event.EventId,
                InternalEventId = result.Event.Id,
                Event = result.Event,
                result.IsReplay,
                result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting event for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while submitting the event" });
        }
    }

    /// <summary>
    /// Updates an existing live score event.
    /// </summary>
    [HttpPut("contests/{contestId}/events/{eventId}")]
    [RequirePermission("scoring.correct")]
    public async Task<IActionResult> UpdateEvent(
        Guid contestId,
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { Message = "A correction reason is required." });
            }

            var contest = await _contestsService.GetContestByIdAsync(contestId);
            var existing = (await _liveScoringService.GetContestScoresAsync(contestId))
                .FirstOrDefault(score => score.Id == eventId || score.EventId == eventId);
            if (existing is null) return NotFound();
            if (contest is not null)
            {
                var policyError = await ValidateCorrectionPolicyAsync(
                    contest, existing.EventType, HttpContext.RequestAborted);
                if (policyError is not null) return BadRequest(new { Message = policyError });
            }
            var correctionRequest = new SubmitEventRequest(
                    existing.EventId,
                    existing.ParticipantId,
                    existing.MetricName,
                    request.NewValue,
                    request.UpdatedBy,
                    existing.SegmentId,
                    existing.ClockSeconds,
                    existing.EventType,
                    existing.SecondaryParticipantId,
                    existing.DeviceTimestamp,
                    PrimaryPlayerId: existing.PrimaryPlayerId,
                    SecondaryPlayerId: existing.SecondaryPlayerId);
            var validationError = contest is null
                ? "The contest was not found."
                : await ValidateEventAgainstRulesetAsync(
                    contest, correctionRequest, HttpContext.RequestAborted);
            if (validationError is not null)
            {
                return BadRequest(new { Message = validationError });
            }

            var actorId = User.UserId();
            if (!actorId.HasValue) return Unauthorized();
            var result = await _liveScoringService.RequestCorrectionAsync(
                contestId,
                eventId,
                new LiveScoreCorrectionProposal(
                    LiveScoreCorrectionKind.Correct,
                    request.Reason,
                    request.NewValue),
                actorId.Value);

            if (!result.Success)
            {
                return BadRequest(new { result.Message });
            }

            return Accepted(result.Correction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event {EventId}", eventId);
            return StatusCode(500, new { Message = "An error occurred while updating the event" });
        }
    }

    /// <summary>
    /// Reverses an event without deleting its audit history.
    /// </summary>
    [HttpPost("contests/{contestId}/events/{eventId}/reverse")]
    [RequirePermission("scoring.correct")]
    public async Task<IActionResult> ReverseEvent(
        Guid contestId,
        Guid eventId,
        [FromBody] ReverseEventRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { Message = "A reversal reason is required." });
        }

        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var existing = (await _liveScoringService.GetContestScoresAsync(contestId))
            .FirstOrDefault(score => score.Id == eventId || score.EventId == eventId);
        if (contest is null || existing is null) return NotFound();
        var policyError = await ValidateCorrectionPolicyAsync(
            contest, existing.EventType, HttpContext.RequestAborted);
        if (policyError is not null) return BadRequest(new { Message = policyError });
        var result = await _liveScoringService.RequestCorrectionAsync(
            contestId,
            eventId,
            new LiveScoreCorrectionProposal(
                LiveScoreCorrectionKind.Reverse,
                request.Reason.Trim()),
            actorId.Value);
        if (!result.Success) return BadRequest(new { result.Message });
        return Accepted(result.Correction);
    }

    [HttpPost("contests/{contestId}/events/{eventId}/corrections")]
    [RequirePermission("scoring.correct")]
    public async Task<IActionResult> RequestCorrection(
        Guid contestId,
        Guid eventId,
        [FromBody] RequestEventCorrectionRequest request)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        if (!Enum.TryParse<LiveScoreCorrectionKind>(request.Kind, true, out var kind))
            return BadRequest(new { Message = "Correction kind must be Correct, Reverse, or Replace." });

        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var existing = (await _liveScoringService.GetContestScoresAsync(contestId))
            .FirstOrDefault(score => score.Id == eventId || score.EventId == eventId);
        if (contest is null || existing is null) return NotFound();
        var policyError = await ValidateCorrectionPolicyAsync(
            contest, existing.EventType, HttpContext.RequestAborted);
        if (policyError is not null) return BadRequest(new { Message = policyError });
        var proposedValue = kind == LiveScoreCorrectionKind.Reverse
            ? 0
            : request.Value ?? existing.Value;
        var correctionRequest = new SubmitEventRequest(
                    existing.EventId,
                    request.ParticipantId ?? existing.ParticipantId,
                    request.MetricName ?? existing.MetricName,
                    proposedValue,
                    null,
                    request.SegmentId ?? existing.SegmentId,
                    request.ClockSeconds ?? existing.ClockSeconds,
                    request.EventType ?? existing.EventType,
                    request.SecondaryParticipantId ?? existing.SecondaryParticipantId,
                    existing.DeviceTimestamp,
                    PrimaryPlayerId: existing.PrimaryPlayerId,
                    SecondaryPlayerId: existing.SecondaryPlayerId);
        var validationError = kind == LiveScoreCorrectionKind.Reverse
            ? null
            : await ValidateEventAgainstRulesetAsync(
                contest, correctionRequest, HttpContext.RequestAborted);
        if (validationError is not null)
            return BadRequest(new { Message = validationError });

        var result = await _liveScoringService.RequestCorrectionAsync(
            contestId,
            eventId,
            new LiveScoreCorrectionProposal(
                kind,
                request.Reason,
                request.Value,
                request.EventType,
                request.ParticipantId,
                request.SecondaryParticipantId,
                request.SegmentId,
                request.MetricName,
                request.ClockSeconds),
            actorId.Value);
        return result.Success ? Accepted(result.Correction) : BadRequest(new { result.Message });
    }

    [HttpGet("contests/{contestId}/corrections")]
    [RequirePermission("scoring.correct")]
    public async Task<IActionResult> GetCorrections(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        return Ok(await _liveScoringService.GetCorrectionsAsync(contestId));
    }

    [HttpPost("contests/{contestId}/corrections/{correctionId}/approve")]
    [RequirePermission("results.approve")]
    public Task<IActionResult> ApproveCorrection(
        Guid contestId,
        Guid correctionId,
        [FromBody] ReviewEventCorrectionRequest request) =>
        ReviewCorrection(contestId, correctionId, true, request.Reason);

    [HttpPost("contests/{contestId}/corrections/{correctionId}/reject")]
    [RequirePermission("results.approve")]
    public Task<IActionResult> RejectCorrection(
        Guid contestId,
        Guid correctionId,
        [FromBody] ReviewEventCorrectionRequest request) =>
        ReviewCorrection(contestId, correctionId, false, request.Reason);

    private async Task<IActionResult> ReviewCorrection(
        Guid contestId,
        Guid correctionId,
        bool approve,
        string? reason)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        if (!approve && string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { Message = "A rejection reason is required." });

        var result = await _liveScoringService.ReviewCorrectionAsync(
            contestId, correctionId, approve, actorId.Value, reason);
        if (!result.Success) return BadRequest(new { result.Message });

        if (approve && result.Event is not null && result.Correction is not null)
        {
            await _derivedState.RebuildAsync(
                contestId,
                actorId.Value,
                $"Approved {result.Correction.Kind.ToString().ToLowerInvariant()} correction {correctionId:N}",
                HttpContext.RequestAborted);
            await _notifier.BroadcastLiveScoreUpdate(
                contestId,
                null,
                new LiveScoreUpdateDto(
                    contestId,
                    result.Event.EventId,
                    result.Event.ParticipantId,
                    result.Event.MetricName,
                    result.Event.Value,
                    result.Event.DisplayValue,
                    $"{result.Correction.Kind}Approved",
                    DateTime.UtcNow));
        }

        return Ok(result.Correction);
    }

    /// <summary>
    /// Gets the current scores for a contest.
    /// </summary>
    [HttpGet("contests/{contestId}/scores")]
    public async Task<IActionResult> GetContestScores(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var scores = await _liveScoringService.GetContestScoresAsync(contestId);
            return Ok(scores.Select(score => new
            {
                Id = score.EventId,
                InternalEventId = score.Id,
                score.TenantId,
                score.ContestId,
                score.EventId,
                score.EventType,
                score.ParticipantId,
                score.SecondaryParticipantId,
                score.PrimaryPlayerId,
                score.SecondaryPlayerId,
                score.SegmentId,
                score.ClockSeconds,
                score.MetricName,
                score.Value,
                score.DisplayValue,
                score.Sequence,
                score.TimelineSequence,
                score.ScorerId,
                score.AssistId,
                score.Metadata,
                score.DeviceId,
                score.IdempotencyKey,
                score.RulesetVersionId,
                score.DeviceTimestamp,
                score.ServerTimestamp,
                score.ScoredAt,
                score.CreatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scores for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while getting scores" });
        }
    }

    [HttpGet("contests/{contestId}/golf-scorecards")]
    public async Task<IActionResult> GetGolfScorecards(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var reviews = await _database.GolfScorecardReviews
            .AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderByDescending(item => item.SubmittedAt)
            .ToArrayAsync(HttpContext.RequestAborted);
        return Ok(reviews);
    }

    [HttpPost("contests/{contestId}/golf-scorecards")]
    public async Task<IActionResult> SubmitGolfScorecardForReview(
        Guid contestId,
        [FromBody] SubmitGolfScorecardReviewRequest request)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var contest = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .SingleAsync(item => item.Id == contestId, HttpContext.RequestAborted);
        var validationError = ValidateGolfScorecard(contest, request);
        if (validationError is not null) return BadRequest(new { Message = validationError });
        if (await HasPublishedGolfScorecardAsync(contest, request))
            return Conflict(new { Message = "This round already has published hole scores." });

        var duplicate = await _database.GolfScorecardReviews.AnyAsync(item =>
            item.ContestId == contestId
            && item.ParticipantId == request.ParticipantId
            && item.RoundNumber == request.RoundNumber
            && item.Status != GolfScorecardReviewStatus.Rejected,
            HttpContext.RequestAborted);
        if (duplicate)
            return Conflict(new { Message = "This golfer already has a pending or approved scorecard for the round." });

        var review = new GolfScorecardReview(
            Guid.NewGuid(),
            contest.TenantId,
            contestId,
            request.ParticipantId,
            request.RoundNumber,
            JsonSerializer.Serialize(request.Holes.OrderBy(item => item.HoleNumber)),
            actorId.Value);
        _database.GolfScorecardReviews.Add(review);
        await _database.SaveChangesAsync(HttpContext.RequestAborted);
        return Accepted(review);
    }

    [HttpPost("contests/{contestId}/golf-scorecards/{reviewId}/approve")]
    [RequirePermission("results.approve")]
    public Task<IActionResult> ApproveGolfScorecard(
        Guid contestId,
        Guid reviewId,
        [FromBody] ReviewGolfScorecardRequest request) =>
        ReviewGolfScorecard(contestId, reviewId, true, request.Notes);

    [HttpPost("contests/{contestId}/golf-scorecards/{reviewId}/reject")]
    [RequirePermission("results.approve")]
    public Task<IActionResult> RejectGolfScorecard(
        Guid contestId,
        Guid reviewId,
        [FromBody] ReviewGolfScorecardRequest request) =>
        ReviewGolfScorecard(contestId, reviewId, false, request.Notes);

    private async Task<IActionResult> ReviewGolfScorecard(
        Guid contestId,
        Guid reviewId,
        bool approve,
        string? notes)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        if (!approve && string.IsNullOrWhiteSpace(notes))
            return BadRequest(new { Message = "Rejection notes are required." });

        var review = await _database.GolfScorecardReviews.SingleOrDefaultAsync(
            item => item.Id == reviewId && item.ContestId == contestId,
            HttpContext.RequestAborted);
        if (review is null) return NotFound();
        if (review.Status != GolfScorecardReviewStatus.Pending)
            return Conflict(new { Message = "Only pending scorecards can be reviewed." });
        if (!approve)
        {
            review.Reject(actorId.Value, notes!);
            await _database.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(review);
        }

        var contest = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .SingleAsync(item => item.Id == contestId, HttpContext.RequestAborted);
        var holes = JsonSerializer.Deserialize<ScorecardHoleRequest[]>(review.HolesJson)
            ?? Array.Empty<ScorecardHoleRequest>();
        var request = new SubmitGolfScorecardReviewRequest(
            review.ParticipantId, review.RoundNumber, holes);
        var validationError = ValidateGolfScorecard(contest, request);
        if (validationError is not null) return BadRequest(new { Message = validationError });
        if (await HasPublishedGolfScorecardAsync(contest, request))
            return Conflict(new { Message = "This round already has published hole scores." });
        var round = contest.Segments.Single(item =>
            item.SegmentType == "Round" && item.SortOrder == review.RoundNumber);
        var holeSegments = round.NestedSegments.ToDictionary(item => item.SortOrder);
        var sequence = await _database.LiveScores
            .Where(item => item.ContestId == contestId)
            .MaxAsync(item => (int?)item.Sequence, HttpContext.RequestAborted) ?? 0;
        LiveScore? lastScore = null;
        foreach (var hole in holes.OrderBy(item => item.HoleNumber))
        {
            lastScore = AddApprovedGolfScore(
                contest, review, holeSegments[hole.HoleNumber].Id,
                "STROKES", "strokes", hole.Strokes, actorId.Value, ++sequence);
            if (hole.Penalties > 0)
            {
                lastScore = AddApprovedGolfScore(
                    contest, review, holeSegments[hole.HoleNumber].Id,
                    "PENALTY_STROKE", "penalties", hole.Penalties,
                    actorId.Value, ++sequence);
            }
        }
        review.Approve(actorId.Value, notes);
        await _database.SaveChangesAsync(HttpContext.RequestAborted);
        await _derivedState.RebuildAsync(
            contestId, actorId.Value, $"Approved golf scorecard {reviewId:N}",
            HttpContext.RequestAborted);
        if (lastScore is not null)
        {
            await _notifier.BroadcastLiveScoreUpdate(
                contestId,
                contest.TenantId,
                new LiveScoreUpdateDto(
                    contestId,
                    lastScore.Id,
                    lastScore.ParticipantId,
                    lastScore.MetricName,
                    lastScore.Value,
                    lastScore.DisplayValue,
                    "ScorecardApproved",
                    DateTime.UtcNow));
        }
        return Ok(review);
    }

    private LiveScore AddApprovedGolfScore(
        Contest contest,
        GolfScorecardReview review,
        Guid segmentId,
        string eventType,
        string metricName,
        decimal value,
        Guid actorId,
        int sequence)
    {
        var eventId = Guid.NewGuid();
        var score = new LiveScore(
            Guid.NewGuid(), contest.Id, eventId, review.ParticipantId,
            metricName, value, segmentId, tenantId: contest.TenantId,
            eventType: eventType, deviceId: "scorecard-review",
            idempotencyKey: $"scorecard-{review.Id:N}-{sequence}",
            rulesetVersionId: contest.RulesetVersionId);
        score.SetSequence(sequence);
        score.SetDisplayValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        score.SetScorer(actorId.ToString(), null);
        _database.LiveScores.Add(score);
        return score;
    }

    private static string? ValidateGolfScorecard(
        Contest contest,
        SubmitGolfScorecardReviewRequest request)
    {
        if (contest.SportId != Guid.Parse("00000000-0000-0000-0000-000000000102"))
            return "The contest is not configured for golf.";
        if (!contest.Participants.Any(item => item.ParticipantId == request.ParticipantId))
            return "The golfer does not belong to this contest.";
        var round = contest.Segments.SingleOrDefault(item =>
            item.SegmentType == "Round" && item.SortOrder == request.RoundNumber);
        if (round is null) return "The selected round is invalid.";
        var holes = request.Holes.OrderBy(item => item.HoleNumber).ToArray();
        if (holes.Length != 18
            || holes.Select(item => item.HoleNumber).Distinct().Count() != 18
            || holes[0].HoleNumber != 1
            || holes[^1].HoleNumber != 18
            || holes.Any(item => item.Strokes is < 1 or > 20
                || item.Penalties is < 0 or > 20))
            return "A scorecard must contain one valid score for every hole 1–18.";
        return null;
    }

    private async Task<bool> HasPublishedGolfScorecardAsync(
        Contest contest,
        SubmitGolfScorecardReviewRequest request)
    {
        var round = contest.Segments.Single(item =>
            item.SegmentType == "Round" && item.SortOrder == request.RoundNumber);
        var holeIds = round.NestedSegments.Select(item => item.Id).ToArray();
        return await _database.LiveScores.AsNoTracking().AnyAsync(item =>
            item.ContestId == contest.Id
            && item.ParticipantId == request.ParticipantId
            && item.MetricName == "strokes"
            && item.SegmentId.HasValue
            && holeIds.Contains(item.SegmentId.Value),
            HttpContext.RequestAborted);
    }

    [HttpGet("contests/{contestId}/state")]
    public async Task<IActionResult> GetContestState(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var events = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.ServerTimestamp)
            .ThenBy(item => item.Id)
            .ToArrayAsync();
        var totals = events
            .GroupBy(item => new { item.ParticipantId, item.MetricName })
            .Select(group => new
            {
                group.Key.ParticipantId,
                group.Key.MetricName,
                Value = group.Sum(item => item.Value)
            })
            .OrderBy(item => item.ParticipantId)
            .ThenBy(item => item.MetricName)
            .ToArray();
        return Ok(new
        {
            ContestId = contestId,
            contest!.TenantId,
            contest.Status,
            contest.RulesetVersionId,
            EventCount = events.Length,
            LastServerTimestamp = events.LastOrDefault()?.ServerTimestamp,
            Totals = totals
        });
    }

    [HttpGet("contests/{contestId}/snapshot")]
    public async Task<IActionResult> GetContestSnapshot(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var snapshot = await _derivedState.GetLatestAsync(contestId, HttpContext.RequestAborted);
        return snapshot is null
            ? NotFound(new { Message = "No derived snapshot has been built for this contest." })
            : Ok(snapshot);
    }

    [HttpPost("contests/{contestId}/snapshot/rebuild")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> RebuildContestSnapshot(
        Guid contestId,
        [FromBody] RebuildContestSnapshotRequest request)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { Message = "A rebuild reason is required." });
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var snapshot = await _derivedState.RebuildAsync(
            contestId,
            actorId.Value,
            request.Reason,
            HttpContext.RequestAborted);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    /// <summary>
    /// Gets the score history for a contest.
    /// </summary>
    [HttpGet("contests/{contestId}/scores/history")]
    public async Task<IActionResult> GetScoreHistory(
        Guid contestId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var history = await _liveScoringService.GetScoreHistoryAsync(contestId, skip, take);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting score history for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while getting score history" });
        }
    }

    /// <summary>
    /// Gets the current live scoring session for a contest.
    /// </summary>
    [HttpGet("contests/{contestId}/session")]
    public async Task<IActionResult> GetSession(Guid contestId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var session = await _liveScoringService.GetActiveSessionAsync(contestId);
            if (session == null)
            {
                return NotFound(new { Message = "No active session found" });
            }
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while getting session" });
        }
    }

    /// <summary>
    /// Starts a live scoring session.
    /// </summary>
    [HttpPost("contests/{contestId}/session/start")]
    public async Task<IActionResult> StartSession(
        Guid contestId,
        [FromBody] StartSessionRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var session = await _liveScoringService.StartSessionAsync(
                contestId,
                request.EventId,
                request.Name);
            var contest = await _contestsService.GetContestByIdAsync(contestId);
            var firstSegment = contest is null
                ? null
                : FlattenSegments(contest)
                    .Select(item => item.Segment)
                    .FirstOrDefault(segment => segment.NestedSegments.Count == 0);
            var schema = contest is null ? null : SchemaFor(contest);
            if (firstSegment is not null)
            {
                await _contestsService.ActivateSegmentAsync(contestId, firstSegment.Id);
                await _liveScoringService.SetSessionContextAsync(
                    contestId,
                    firstSegment.Id,
                    0,
                    schema?.ClockCountsDown == true);
            }

            await _notifier.BroadcastContestStatusChange(
                contestId,
                tenantId,
                "Active");
            await _notifier.BroadcastContestStarted(contestId, tenantId);

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while starting session" });
        }
    }

    [HttpPut("contests/{contestId}/session/context")]
    public async Task<IActionResult> SetSessionContext(
        Guid contestId,
        [FromBody] SetSessionContextRequest request,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        if (request.ClockSeconds < 0)
            return BadRequest(new { Message = "Clock seconds cannot be negative." });

        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var selectedSegment = contest is null
            ? null
            : FlattenSegments(contest)
                .Select(item => item.Segment)
                .FirstOrDefault(item => item.Id == request.SegmentId);
        if (contest is null || selectedSegment is null)
        {
            return BadRequest(new { Message = "The segment does not belong to this contest." });
        }
        var schema = SchemaFor(contest);
        if (selectedSegment.IsOptional
            && selectedSegment.ActivationCondition is "tie_after_regulation"
                or "tie_after_extra_time")
        {
            var includeExtraTime = selectedSegment.ActivationCondition == "tie_after_extra_time";
            var scoringSegments = FlattenSegments(contest)
                .Select(item => item.Segment)
                .Where(item => item.SortOrder < selectedSegment.SortOrder
                    && item.SegmentType is not ("Break" or "Shootout")
                    && (includeExtraTime || !item.IsOptional))
                .OrderBy(item => item.SortOrder)
                .ToArray();
            if (scoringSegments.Length == 0
                || scoringSegments[^1].Status == SegmentStatus.Pending)
            {
                return BadRequest(new
                {
                    Message = includeExtraTime
                        ? "A shootout can only begin after extra time reaches its final period."
                        : "Overtime can only begin after regulation reaches its final period."
                });
            }

            var persistedTotals = await _database.LiveScores
                .AsNoTracking()
                .Where(item => item.ContestId == contestId
                    && item.MetricName == schema!.PrimaryMetric
                    && item.SegmentId.HasValue
                    && scoringSegments.Select(segment => segment.Id)
                        .Contains(item.SegmentId!.Value))
                .GroupBy(item => item.ParticipantId)
                .Select(group => new
                {
                    ParticipantId = group.Key,
                    Total = group.Sum(item => item.Value)
                })
                .ToDictionaryAsync(
                    item => item.ParticipantId,
                    item => item.Total);
            var totals = contest.Participants
                .OrderBy(item => item.SortOrder)
                .Select(item => persistedTotals.GetValueOrDefault(item.ParticipantId))
                .ToArray();
            if (persistedTotals.Count == 0
                || totals.Length != 2
                || totals.Distinct().Count() != 1)
            {
                return BadRequest(new
                {
                    Message = includeExtraTime
                        ? "A shootout is available only when both teams are tied after extra time."
                        : "Overtime is available only when both teams are tied after regulation."
                });
            }
        }

        if (request.ClockSeconds != 0 && schema?.SupportsClock != true)
            return BadRequest(new { Message = $"{schema?.Name ?? "This sport"} does not support a clock." });

        await _contestsService.ActivateSegmentAsync(contestId, request.SegmentId);
        var result = await _liveScoringService.SetSessionContextAsync(
            contestId,
            request.SegmentId,
            request.ClockSeconds,
            schema?.ClockCountsDown == true);
        if (!result.Success) return BadRequest(new { result.Message });
        await _notifier.BroadcastContestStatusChange(contestId, tenantId, "ContextUpdated");
        return Ok(new { result.Message });
    }

    [HttpPost("contests/{contestId}/session/clock/start")]
    public async Task<IActionResult> StartClock(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var result = await _liveScoringService.StartClockAsync(contestId);
        if (!result.Success) return BadRequest(new { result.Message });
        await _notifier.BroadcastContestStatusChange(contestId, tenantId, "ClockStarted");
        return Ok(new { result.Message });
    }

    [HttpPost("contests/{contestId}/session/clock/pause")]
    public async Task<IActionResult> PauseClock(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var result = await _liveScoringService.PauseClockAsync(contestId);
        if (!result.Success) return BadRequest(new { result.Message });
        await _notifier.BroadcastContestStatusChange(contestId, tenantId, "ClockPaused");
        return Ok(new { result.Message });
    }

    [HttpPost("contests/{contestId}/session/segment/next")]
    public async Task<IActionResult> AdvanceSegment(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var session = await _liveScoringService.GetActiveSessionAsync(contestId);
        if (contest is null || session?.ActiveSegmentId is null)
            return BadRequest(new { Message = "An active segment is required." });

        var leaves = FlattenSegments(contest)
            .Select(item => item.Segment)
            .Where(segment =>
                segment.NestedSegments.Count == 0 &&
                !segment.IsOptional)
            .ToArray();
        var currentIndex = Array.FindIndex(
            leaves,
            segment => segment.Id == session.ActiveSegmentId);
        if (currentIndex < 0 || currentIndex + 1 >= leaves.Length)
            return BadRequest(new
            {
                Message = "There is no next required segment. Conditional segments must be activated explicitly."
            });

        var current = leaves[currentIndex];
        var next = leaves[currentIndex + 1];
        if (session.IsClockRunning)
            await _liveScoringService.PauseClockAsync(contestId);
        var segmentScores = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId
                && item.SegmentId == current.Id)
            .GroupBy(item => item.ParticipantId)
            .Select(group => new
            {
                ParticipantId = group.Key,
                Score = group.Sum(item => item.Value)
            })
            .ToDictionaryAsync(item => item.ParticipantId, item => item.Score);
        await _notifier.BroadcastSegmentEnd(
            contestId,
            current.Id,
            tenantId,
            new SegmentEndDto(
                current.Id,
                current.Name,
                DateTime.UtcNow,
                segmentScores));
        await _contestsService.ActivateSegmentAsync(contestId, next.Id);
        var schema = SchemaFor(contest);
        var result = await _liveScoringService.SetSessionContextAsync(
            contestId,
            next.Id,
            0,
            schema?.ClockCountsDown == true);
        if (!result.Success) return BadRequest(new { result.Message });
        await _notifier.BroadcastContestStatusChange(
            contestId,
            tenantId,
            "SegmentAdvanced");
        return Ok(new
        {
            result.Message,
            SegmentId = next.Id,
            next.Name
        });
    }

    /// <summary>
    /// Pauses the current live scoring session.
    /// </summary>
    [HttpPost("contests/{contestId}/session/pause")]
    public async Task<IActionResult> PauseSession(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var result = await _liveScoringService.PauseSessionAsync(contestId);
            if (!result.Success)
            {
                return BadRequest(new { result.Message });
            }

            await _notifier.BroadcastContestStatusChange(contestId, tenantId, "Paused");

            return Ok(new { result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing session for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while pausing session" });
        }
    }

    /// <summary>
    /// Resumes the current live scoring session.
    /// </summary>
    [HttpPost("contests/{contestId}/session/resume")]
    public async Task<IActionResult> ResumeSession(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var result = await _liveScoringService.ResumeSessionAsync(contestId);
            if (!result.Success)
            {
                return BadRequest(new { result.Message });
            }

            await _notifier.BroadcastContestStatusChange(contestId, tenantId, "Active");

            return Ok(new { result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming session for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while resuming session" });
        }
    }

    /// <summary>
    /// Ends the current live scoring session.
    /// </summary>
    [HttpPost("contests/{contestId}/session/end")]
    public async Task<IActionResult> EndSession(
        Guid contestId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId)
    {
        var accessFailure = await ContestAccessFailure(contestId);
        if (accessFailure is not null) return accessFailure;
        try
        {
            var result = await _liveScoringService.EndSessionAsync(contestId);
            if (!result.Success)
            {
                return BadRequest(new { result.Message });
            }

            await _notifier.BroadcastContestStatusChange(contestId, tenantId, "Ended");

            return Ok(new { result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session for contest {ContestId}", contestId);
            return StatusCode(500, new { Message = "An error occurred while ending session" });
        }
    }

    private async Task<IActionResult?> ContestAccessFailure(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        return User.CanAccessTenant(contest.TenantId) ? null : Forbid();
    }

    private static string? GetFormatName(string? validationConfiguration)
    {
        if (string.IsNullOrWhiteSpace(validationConfiguration)) return null;
        try
        {
            using var document = JsonDocument.Parse(validationConfiguration);
            return document.RootElement.TryGetProperty("formatName", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ScoringSchema? SchemaFor(
        SportsPlatform.Modules.Contests.Entities.Contest contest)
    {
        var segments = FlattenSegments(contest)
            .Select(item => new ScoringSegmentSchema(
                item.Segment.Id,
                item.Segment.Name,
                item.Segment.SegmentType,
                item.Segment.SortOrder,
                item.Segment.Status.ToString(),
                item.ParentSegmentId,
                item.Depth,
                item.Segment.IsOptional,
                item.Segment.ActivationCondition))
            .ToArray();
        return ScoringSchemaCatalog.ForSport(contest.SportId, segments)
            ?? new ScoringSchema(
                contest.SportId.ToString("N"),
                "Configured sport",
                "score",
                "score",
                false,
                false,
                false,
                Array.Empty<ScoringActionSchema>(),
                segments);
    }

    private static ScoringSchema WithEventDefinitions(
        ScoringSchema schema,
        IReadOnlyList<EventDefinition> definitions)
    {
        var additions = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.RequiredMetric))
            .Where(definition => !schema.Actions.Any(action =>
                string.Equals(action.EventType, definition.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(definition =>
            {
                var minimum = definition.Validation.MinimumValue;
                var maximum = definition.Validation.MaximumValue;
                var defaultValue = minimum == maximum && minimum.HasValue
                    ? minimum.Value
                    : Math.Clamp(1m, minimum ?? 1m, maximum ?? 1m);
                return new ScoringActionSchema(
                    definition.Code.ToLowerInvariant(),
                    definition.Name,
                    definition.RequiredMetric!,
                    defaultValue,
                    minimum.HasValue || maximum.HasValue ? "number" : "fixed",
                    minimum,
                    maximum,
                    1,
                    EventType: definition.Code,
                    RequiresParticipant: definition.RequiresParticipant,
                    RequiresSegment: !definition.SegmentApplicability.Contains(
                        "*", StringComparer.OrdinalIgnoreCase),
                    RequiresPlayer: definition.RequiresSecondaryParticipant);
            });
        return schema with
        {
            Actions = schema.Actions.Concat(additions).ToArray(),
            EventDefinitions = definitions,
            PrimaryMetric = definitions.Select(item => item.RequiredMetric)
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? schema.PrimaryMetric,
            SupportsClock = schema.SupportsClock || definitions.Any(item =>
                item.TimingRequirement is EventTimingRequirement.Clock
                    or EventTimingRequirement.TimestampOrClock)
        };
    }

    private static string? ValidateConfiguredEvent(
        EventDefinition definition,
        SubmitEventRequest request,
        Contest contest)
    {
        if (definition.Code.Equals("SUBSTITUTION", StringComparison.OrdinalIgnoreCase)
            && (!request.PrimaryPlayerId.HasValue || !request.SecondaryPlayerId.HasValue))
            return "A substitution requires both the player leaving and the player entering.";
        if (definition.Code.Equals("SUBSTITUTION", StringComparison.OrdinalIgnoreCase)
            && request.PrimaryPlayerId == request.SecondaryPlayerId)
            return "The player leaving and the player entering must be different.";
        if (definition.RequiresParticipant
            && !contest.Participants.Any(item => item.ParticipantId == request.ParticipantId))
            return "The participant does not belong to this contest.";
        if (!definition.RequiresParticipant
            && request.ParticipantId != Guid.Empty
            && !contest.Participants.Any(item => item.ParticipantId == request.ParticipantId))
            return "The participant does not belong to this contest.";
        var segment = request.SegmentId.HasValue
            ? FlattenSegments(contest).Select(item => item.Segment)
                .FirstOrDefault(item => item.Id == request.SegmentId.Value)
            : null;
        if (request.SegmentId.HasValue && segment is null)
            return "The selected segment does not belong to this contest.";
        if (request.ClockSeconds is < 0)
            return "Clock seconds cannot be negative.";

        if (definition.Validation.MinimumValue is null
            && definition.Validation.MaximumValue is null)
        {
            var schemaAction = SchemaFor(contest)?.Actions.FirstOrDefault(action =>
                !string.IsNullOrWhiteSpace(action.EventType)
                && action.EventType.Equals(
                    definition.Code,
                    StringComparison.OrdinalIgnoreCase)
                && action.Metric.Equals(
                    request.MetricName,
                    StringComparison.OrdinalIgnoreCase));

            if (schemaAction is not null)
            {
                if (schemaAction.InputType == "number")
                {
                    if (schemaAction.Minimum is not null
                        && request.Value < schemaAction.Minimum)
                        return $"{schemaAction.Label} must be at least {schemaAction.Minimum}.";
                    if (schemaAction.Maximum is not null
                        && request.Value > schemaAction.Maximum)
                        return $"{schemaAction.Label} must not exceed {schemaAction.Maximum}.";
                }
                else if (request.Value != schemaAction.DefaultValue)
                {
                    return $"Value {request.Value} is not allowed for event '{definition.Code}'.";
                }
            }
        }

        try
        {
            EventFramework.ValidatePayload(definition, new EventPayload(
                request.ParticipantId,
                request.SecondaryParticipantId,
                request.MetricName,
                request.Value,
                segment?.Id.ToString(),
                segment?.Name,
                segment?.SegmentType,
                request.ClockSeconds,
                request.DeviceTimestamp?.ToUniversalTime()));
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private static string? ValidateEvent(
        ScoringSchema? schema,
        SubmitEventRequest request,
        SportsPlatform.Modules.Contests.Entities.Contest? contest)
    {
        if (schema is null)
        {
            return "No scoring schema is configured for this sport.";
        }

        var matchingActions = schema.Actions
            .Where(action => string.Equals(
                action.Metric,
                request.MetricName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingActions.Length == 0)
        {
            return $"Metric '{request.MetricName}' is not allowed for {schema.Name}.";
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var typedAction = schema.Actions.FirstOrDefault(action =>
                !string.IsNullOrWhiteSpace(action.EventType)
                && string.Equals(
                    action.EventType,
                    request.EventType,
                    StringComparison.OrdinalIgnoreCase));
            if (typedAction is not null
                && !string.Equals(
                    typedAction.Metric,
                    request.MetricName,
                    StringComparison.OrdinalIgnoreCase))
                return $"Event type '{request.EventType}' is not valid for metric '{request.MetricName}'.";
            if (typedAction is not null)
                matchingActions = new[] { typedAction };
        }

        if (contest is null ||
            !contest.Participants.Any(participant =>
                participant.ParticipantId == request.ParticipantId))
        {
            return "The participant does not belong to this contest.";
        }
        if (request.SecondaryParticipantId == request.ParticipantId)
            return "The primary and secondary participants must be different.";

        if (matchingActions.Any(action => action.RequiresSegment))
        {
            if (request.SegmentId is null)
            {
                return "A contest segment is required for this event.";
            }
            if (!FlattenSegments(contest).Any(item =>
                    item.Segment.Id == request.SegmentId))
            {
                return "The selected segment does not belong to this contest.";
            }
            if (contest.SportId == Guid.Parse(
                    "00000000-0000-0000-0000-000000000102"))
            {
                var round = contest.Segments.FirstOrDefault(item =>
                    item.Id == request.SegmentId
                    || item.NestedSegments.Any(hole =>
                        hole.Id == request.SegmentId));
                var participant = contest.Participants.First(item =>
                    item.ParticipantId == request.ParticipantId);
                if (round is not null
                    && round.SortOrder > contest.CutAfterRound
                    && contest.CutAppliedAt is null)
                {
                    return "The cut must be applied before round three scoring.";
                }
                if (round is not null
                    && round.SortOrder > contest.CutAfterRound
                    && participant.MadeCut != true)
                {
                    return "The golfer did not make the cut.";
                }
            }
        }

        if (request.ClockSeconds is < 0)
        {
            return "Clock seconds cannot be negative.";
        }
        if (request.ClockSeconds is not null && !schema.SupportsClock)
        {
            return $"{schema.Name} does not support a scoring clock.";
        }

        var numberAction = matchingActions.FirstOrDefault(action =>
            action.InputType == "number");
        if (numberAction is not null)
        {
            if (numberAction.Minimum is not null &&
                request.Value < numberAction.Minimum)
            {
                return $"{numberAction.Label} must be at least {numberAction.Minimum}.";
            }
            if (numberAction.Maximum is not null &&
                request.Value > numberAction.Maximum)
            {
                return $"{numberAction.Label} must not exceed {numberAction.Maximum}.";
            }
            return null;
        }

        return matchingActions.Any(action => action.DefaultValue == request.Value)
            ? null
            : $"Value {request.Value} is not allowed for metric '{request.MetricName}'.";
    }

    private async Task<string?> ValidateSecondaryParticipantAsync(
        Contest contest,
        Guid primaryParticipantId,
        Guid? secondaryParticipantId,
        Guid? primaryPlayerId,
        Guid? secondaryPlayerId)
    {
        var playerIds = new[] { secondaryParticipantId, primaryPlayerId, secondaryPlayerId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        if (playerIds.Length == 0) return null;
        if (primaryPlayerId.HasValue && primaryPlayerId == secondaryPlayerId)
            return "Primary and secondary players must be different.";
        var participantIds = contest.Participants.Select(item => item.ParticipantId).ToHashSet();
        var rosterPlayerIds = playerIds.Where(id => !participantIds.Contains(id)).ToArray();
        if (rosterPlayerIds.Length == 0) return null;

        var activeRosterPlayers = await _database.Set<TeamRoster>()
            .AsNoTracking()
            .Where(roster => roster.TenantId == contest.TenantId
                && roster.TeamId == primaryParticipantId
                && roster.IsActive)
            .SelectMany(roster => roster.Entries)
            .Where(entry => entry.IsActive && rosterPlayerIds.Contains(entry.AthleteId))
            .Select(entry => entry.AthleteId)
            .Distinct()
            .ToArrayAsync(HttpContext.RequestAborted);
        return rosterPlayerIds.All(activeRosterPlayers.Contains)
            ? null
            : "Every selected player must belong to an active roster for this team.";
    }

    private static IEnumerable<(
        ContestSegment Segment,
        Guid? ParentSegmentId,
        int Depth)> FlattenSegments(Contest contest)
    {
        foreach (var parent in contest.Segments.OrderBy(segment => segment.SortOrder))
        {
            yield return (parent, null, 0);
            foreach (var nested in parent.NestedSegments.OrderBy(segment => segment.SortOrder))
            {
                yield return (nested, parent.Id, 1);
            }
        }
    }

    private async Task<(IReadOnlyList<EventDefinition> Definitions, EventDefinition? Definition, string? Error)>
        ResolveEventDefinitionsAsync(
        Contest contest,
        string? explicitEventType,
        bool pinRuleset,
        CancellationToken cancellationToken)
    {
        var sport = await _database.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == contest.SportId, cancellationToken);
        if (sport is null)
            return (Array.Empty<EventDefinition>(), null, "The contest sport was not found.");
        var ruleset = contest.RulesetVersionId.HasValue
            ? sport.Versions.SingleOrDefault(item => item.Id == contest.RulesetVersionId.Value)
            : sport.Versions.Where(item => item.IsPublished)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefault();
        if (ruleset is null)
            return (Array.Empty<EventDefinition>(), null,
                contest.RulesetVersionId.HasValue
                    ? "The contest's assigned ruleset version was not found."
                    : null); // Legacy contests continue to use the immutable code schema.
        if (!ruleset.IsPublished)
            return (Array.Empty<EventDefinition>(), null,
                "The contest's assigned ruleset version is not published.");
        if (pinRuleset && !contest.RulesetVersionId.HasValue)
        {
            contest.AssignRulesetVersion(ruleset.Id);
            await _database.SaveChangesAsync(cancellationToken);
        }
        try
        {
            var definitions = EventFramework.ParseDefinitions(ruleset.EventDefinitions);
            if (string.IsNullOrWhiteSpace(explicitEventType))
                return (definitions, null, null);
            var definition = definitions.FirstOrDefault(item =>
                item.Code.Equals(explicitEventType.Trim(), StringComparison.OrdinalIgnoreCase));
            return definition is null
                ? (definitions, null,
                    $"Event type '{explicitEventType}' is not allowed by ruleset version {ruleset.VersionNumber}.")
                : (definitions, definition, null);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return (Array.Empty<EventDefinition>(), null,
                "The contest ruleset contains invalid event definitions.");
        }
    }

    private async Task<string?> ValidateCorrectionPolicyAsync(
        Contest contest,
        string eventType,
        CancellationToken cancellationToken)
    {
        var ruleset = await ResolveEventDefinitionsAsync(
            contest, null, false, cancellationToken);
        if (ruleset.Error is not null)
            return ruleset.Error;
        var definition = ruleset.Definitions.FirstOrDefault(item =>
            item.Code.Equals(eventType, StringComparison.OrdinalIgnoreCase));
        return definition?.CorrectionPolicy == EventCorrectionPolicy.Prohibited
            ? $"Event '{definition.Code}' does not allow corrections."
            : null;
    }

    private async Task<string?> ValidateEventAgainstRulesetAsync(
        Contest contest,
        SubmitEventRequest request,
        CancellationToken cancellationToken)
    {
        var ruleset = await ResolveEventDefinitionsAsync(
            contest, null, false, cancellationToken);
        if (ruleset.Error is not null)
            return ruleset.Error;
        var definition = string.IsNullOrWhiteSpace(request.EventType)
            ? null
            : ruleset.Definitions.FirstOrDefault(item =>
                item.Code.Equals(request.EventType, StringComparison.OrdinalIgnoreCase));
        return definition is not null
            ? ValidateConfiguredEvent(definition, request, contest)
            : ValidateEvent(SchemaFor(contest), request, contest);
    }
}

/// <summary>
/// Request for submitting an event.
/// </summary>
public record SubmitEventRequest(
    Guid EventId,
    Guid ParticipantId,
    string MetricName,
    decimal Value,
    string? ScoredBy,
    Guid? SegmentId = null,
    decimal? ClockSeconds = null,
    string? EventType = null,
    Guid? SecondaryParticipantId = null,
    DateTime? DeviceTimestamp = null,
    string? DeviceId = null,
    int? SequenceNumber = null,
    string? IdempotencyKey = null,
    Guid? PrimaryPlayerId = null,
    Guid? SecondaryPlayerId = null);

/// <summary>
/// Request for updating an event.
/// </summary>
public record UpdateEventRequest(
    decimal NewValue,
    string? UpdatedBy,
    string? Reason);

public record ReverseEventRequest(
    string Reason,
    string? ReversedBy);

public record RequestEventCorrectionRequest(
    string Kind,
    string Reason,
    decimal? Value = null,
    string? EventType = null,
    Guid? ParticipantId = null,
    Guid? SecondaryParticipantId = null,
    Guid? SegmentId = null,
    string? MetricName = null,
    decimal? ClockSeconds = null);

public record ReviewEventCorrectionRequest(string? Reason);

public sealed record SubmitGolfScorecardReviewRequest(
    Guid ParticipantId,
    int RoundNumber,
    IReadOnlyList<ScorecardHoleRequest> Holes);

public sealed record ScorecardHoleRequest(
    int HoleNumber,
    int Strokes,
    int Penalties = 0);

public sealed record ReviewGolfScorecardRequest(string? Notes);

public record RebuildContestSnapshotRequest(string Reason);

/// <summary>
/// Request for starting a session.
/// </summary>
public record StartSessionRequest(
    Guid EventId,
    string Name);

public record SetSessionContextRequest(
    Guid SegmentId,
    decimal ClockSeconds);
