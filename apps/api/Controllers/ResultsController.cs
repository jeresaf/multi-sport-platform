using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Results.Services;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Standings;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Api.Hubs;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Authorize]
[RequirePermission("results.manage")]
public class ResultsController : ControllerBase
{
    private static readonly Guid RugbySportId =
        Guid.Parse("00000000-0000-0000-0000-000000000106");
    private readonly IResultService _resultService;
    private readonly IContestsService _contestsService;
    private readonly ILiveScoringService _liveScoringService;
    private readonly SportsPlatformDbContext _db;
    private readonly ContestDerivedStateService _derivedState;
    private readonly ContestHubNotifier _notifier;

    public ResultsController(
        IResultService resultService,
        IContestsService contestsService,
        ILiveScoringService liveScoringService,
        SportsPlatformDbContext db,
        ContestDerivedStateService derivedState,
        ContestHubNotifier notifier)
    {
        _resultService = resultService;
        _contestsService = contestsService;
        _liveScoringService = liveScoringService;
        _db = db;
        _derivedState = derivedState;
        _notifier = notifier;
    }

    [HttpPost]
    public async Task<IActionResult> CreateResult([FromBody] CreateResultRequest request)
    {
        var result = await _resultService.CreateResultAsync(
            request.ContestId,
            request.EventId,
            request.ParticipantId);

        return CreatedAtAction(nameof(GetResult), new { id = result.Id }, new
        {
            result.Id,
            result.ContestId,
            result.EventId,
            result.ParticipantId,
            result.Status,
            result.CreatedAt
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetResult(Guid id)
    {
        var result = await _resultService.GetResultByIdAsync(id);
        if (result == null)
            return NotFound();
        var contest = await _contestsService.GetContestByIdAsync(result.ContestId);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        return Ok(new
        {
            result.Id,
            result.ContestId,
            result.EventId,
            result.ParticipantId,
            result.Status,
            result.Rank,
            result.Position,
            result.Score,
            result.ScoreDisplay,
            result.Time,
            result.PerformanceData,
            result.Statistics,
            result.IsPersonalBest,
            result.IsSeasonBest,
            result.IsCompetitionRecord,
            result.Remarks,
            result.ApprovedBy,
            result.ApprovedAt,
            result.CreatedAt,
            result.UpdatedAt
        });
    }

    [HttpGet("contest/{contestId:guid}")]
    public async Task<IActionResult> GetResultsByContest(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        var results = await _resultService.GetResultsByContestAsync(contestId);
        return Ok(results.Select(r => new
        {
            r.Id,
            r.EventId,
            r.ParticipantId,
            r.Status,
            r.Rank,
            r.Score,
            r.ScoreDisplay,
            r.Time,
            r.Remarks,
            r.ApprovedBy,
            r.ApprovedAt
        }));
    }

    [HttpPost("contest/{contestId:guid}/submit")]
    [RequirePermission("scoring.manage")]
    [RequirePermission("results.submit")]
    public async Task<IActionResult> SubmitContestResults(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        var schema = ScoringSchemaCatalog.ForSport(
            contest.SportId,
            Array.Empty<ScoringSegmentSchema>());
        if (schema is null)
            return BadRequest(new { Message = "No scoring schema is configured for this sport." });

        var scores = await _liveScoringService.GetContestScoresAsync(contestId);
        var grouped = scores
            .Where(score => string.Equals(
                score.MetricName,
                schema.PrimaryMetric,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(score => score.ParticipantId)
            .Select(group => new
            {
                ParticipantId = group.Key,
                Score = group.Sum(score => score.Value),
                EventId = group.OrderByDescending(score => score.Sequence).First().Id
            })
            .ToArray();
        if (grouped.Length == 0)
            return BadRequest(new
            {
                Message = $"No '{schema.PrimaryMetric}' scores are available to submit."
            });

        var existing = await _resultService.GetResultsByContestAsync(contestId);
        if (existing.Any(result =>
                result.Status is ContestResultStatus.UnderReview
                    or ContestResultStatus.Approved
                    or ContestResultStatus.Published
                    or ContestResultStatus.Official))
        {
            return Conflict(new
            {
                Message = "Results under review or already approved/published/official cannot be resubmitted."
            });
        }

        var ordered = schema.LowerIsBetter
            ? grouped.OrderBy(item => item.Score).ToArray()
            : grouped.OrderByDescending(item => item.Score).ToArray();
        var submitted = new List<object>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var entry = ordered[index];
            var result = existing.FirstOrDefault(item =>
                item.ParticipantId == entry.ParticipantId);
            result ??= await _resultService.CreateResultAsync(
                contestId,
                entry.EventId,
                entry.ParticipantId);
            await _resultService.UpdateScoreAsync(
                result.Id,
                entry.Score,
                $"{entry.Score} {schema.ScoreLabel}",
                null);
            await _resultService.UpdateRankAsync(
                result.Id,
                index + 1,
                (index + 1).ToString());
            result = await _resultService.SubmitResultAsync(result.Id);
            submitted.Add(new
            {
                result.Id,
                result.ParticipantId,
                result.Score,
                result.ScoreDisplay,
                result.Rank,
                Status = result.Status.ToString()
            });
        }
        // Legacy generic contests may receive their first lifecycle signal with
        // the first accepted score. Recover that missing start explicitly; all
        // subsequent transitions still use the normal lifecycle graph.
        if (contest.Status == ContestStatus.Draft)
        {
            var recovery = new Dictionary<ContestStatus, IReadOnlySet<ContestStatus>>
            {
                [ContestStatus.Draft] = new HashSet<ContestStatus>
                {
                    ContestStatus.InProgress
                }
            };
            contest.TransitionTo(ContestStatus.InProgress, recovery);
        }
        else if (contest.Status == ContestStatus.CheckInOpen)
        {
            contest.MarkReady();
            contest.Start();
        }
        else if (contest.Status is ContestStatus.Scheduled or ContestStatus.Ready)
        {
            contest.Start();
        }
        if (contest.Status is ContestStatus.InProgress or ContestStatus.Paused)
            contest.Complete();
        await _contestsService.UpdateContestAsync(contest);

        return Ok(new
        {
            ContestId = contestId,
            Status = "Submitted",
            Results = submitted
        });
    }

    [HttpGet("event/{eventId:guid}")]
    public async Task<IActionResult> GetResultsByEvent(Guid eventId)
    {
        var results = await _resultService.GetResultsByEventAsync(eventId);
        return Ok(results.Select(r => new
        {
            r.Id,
            r.ContestId,
            r.ParticipantId,
            r.Status,
            r.Rank,
            r.Score,
            r.ScoreDisplay,
            r.Time
        }));
    }

    [HttpGet("participant/{participantId:guid}")]
    public async Task<IActionResult> GetResultsByParticipant(Guid participantId)
    {
        var results = await _resultService.GetResultsByParticipantAsync(participantId);
        return Ok(results.Select(r => new
        {
            r.Id,
            r.ContestId,
            r.EventId,
            r.Status,
            r.Rank,
            r.Score,
            r.ScoreDisplay,
            r.Time
        }));
    }

    [HttpPut("{id:guid}/score")]
    public async Task<IActionResult> UpdateScore(Guid id, [FromBody] UpdateScoreRequest request)
    {
        var result = await _resultService.UpdateScoreAsync(id, request.Score, request.ScoreDisplay, request.Time);
        return Ok(new { result.Id, result.Score, result.ScoreDisplay, result.Time });
    }

    [HttpPost("{id:guid}/correct")]
    [RequirePermission("scoring.correct")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> CorrectResult(
        Guid id,
        [FromBody] CorrectResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { Message = "A result correction reason is required." });
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        if (current.Result.Status != ContestResultStatus.Corrected)
            return Conflict(new
            {
                Message = "The result must be reopened for correction before its values can change."
            });
        var contest = await _contestsService.GetContestByIdAsync(current.Result.ContestId);
        if (contest is null) return NotFound();
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();

        var oldValue = JsonSerializer.Serialize(new
        {
            current.Result.Score,
            current.Result.ScoreDisplay,
            current.Result.Time,
            current.Result.Rank,
            current.Result.Position,
            current.Result.Status
        });
        var result = await _resultService.UpdateScoreAsync(
            id, request.Score, request.ScoreDisplay, request.Time);
        result = await _resultService.UpdateRankAsync(id, request.Rank, request.Position);
        var newValue = JsonSerializer.Serialize(new
        {
            result.Score,
            result.ScoreDisplay,
            result.Time,
            result.Rank,
            result.Position,
            result.Status
        });
        var audit = new AuditEntry(
            Guid.NewGuid(),
            contest.TenantId,
            actorId,
            "ResultCorrected",
            nameof(ContestResult),
            id.ToString(),
            oldValue,
            newValue,
            request.Reason.Trim());
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _db.AuditEntries.Add(audit);
        await _db.SaveChangesAsync();
        await RebuildStandingsAsync(result.ContestId);
        await _derivedState.RebuildAsync(
            result.ContestId,
            actorId.Value,
            $"Corrected result {result.Id:N}: {request.Reason.Trim()}");

        return Ok(new
        {
            result.Id,
            result.Score,
            result.ScoreDisplay,
            result.Time,
            result.Rank,
            result.Position,
            result.Status,
            CorrectionReason = request.Reason.Trim(),
            CorrectedBy = actorId,
            AuditId = audit.Id
        });
    }

    [HttpPut("{id:guid}/rank")]
    public async Task<IActionResult> UpdateRank(Guid id, [FromBody] UpdateRankRequest request)
    {
        var result = await _resultService.UpdateRankAsync(id, request.Rank, request.Position);
        return Ok(new { result.Id, result.Rank, result.Position });
    }

    [HttpPut("{id:guid}/performance")]
    public async Task<IActionResult> UpdatePerformance(Guid id, [FromBody] UpdatePerformanceRequest request)
    {
        var result = await _resultService.UpdatePerformanceDataAsync(id, request.PerformanceData, request.Statistics);
        return Ok(new { result.Id, result.PerformanceData, result.Statistics });
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission("results.submit")]
    public async Task<IActionResult> SubmitResult(Guid id)
    {
        var result = await _resultService.SubmitResultAsync(id);
        return Ok(new { result.Id, result.Status });
    }

    [HttpPost("{id:guid}/review")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> BeginReview(Guid id)
    {
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var originalStatus = current.Result.Status;
        try
        {
            var result = await _resultService.BeginReviewAsync(id);
            await RecordWorkflowAuditAsync(
                result,
                actorId.Value,
                "ResultReviewStarted",
                originalStatus,
                result.Status,
                null);
            return Ok(new { result.Id, result.Status });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { Message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> ApproveResult(Guid id, [FromBody] ApproveResultRequest request)
    {
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var approvedBy))
            return Unauthorized(new { Message = "The authenticated user ID is invalid." });
        var result = await _resultService.ApproveResultAsync(id, approvedBy);
        var contestResults = await _resultService.GetResultsByContestAsync(
            result.ContestId);
        if (contestResults.All(item => IsApprovedResultStatus(item.Status)))
        {
            var contest = await _contestsService.GetContestByIdAsync(
                result.ContestId);
            if (contest is not null)
            {
                var winner = contestResults
                    .OrderBy(item => item.Rank ?? int.MaxValue)
                    .ThenByDescending(item => item.Score)
                    .First();
                contest.SetWinner(winner.ParticipantId);
                await _contestsService.UpdateContestAsync(contest);
                var advanced = await AdvanceBracketAsync(contest, winner.ParticipantId);
                await _notifier.BroadcastContestResult(
                    contest.Id,
                    contest.TenantId,
                    new ContestResultDto(
                        contest.Id,
                        "Final",
                        DateTime.UtcNow,
                        contestResults
                            .OrderBy(item => item.Rank ?? int.MaxValue)
                            .Select(item => new ParticipantResultDto(
                                item.ParticipantId,
                                item.Rank ?? 0,
                                item.Score ?? 0,
                                null,
                                new Dictionary<string, decimal>()))
                            .ToList()));
                if (advanced)
                    await _notifier.BroadcastQualification(
                        contest.Id,
                        winner.ParticipantId,
                        contest.TenantId);
            }
        }
        await RebuildStandingsAsync(result.ContestId);
        await _derivedState.RebuildAsync(
            result.ContestId,
            approvedBy,
            $"Approved result {result.Id:N}");
        return Ok(new { result.Id, result.Status, result.ApprovedBy, result.ApprovedAt });
    }

    [HttpPost("{id:guid}/reject")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> RejectResult(Guid id, [FromBody] RejectResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { Message = "A rejection reason is required." });
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        var result = await _resultService.RejectResultAsync(id, request.Reason);
        return Ok(new { result.Id, result.Status, result.Remarks });
    }

    [HttpPost("{id:guid}/publish")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> PublishResult(Guid id)
    {
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        var result = await _resultService.PublishResultAsync(id);
        var actorId = User.UserId();
        if (actorId.HasValue)
            await _derivedState.RebuildAsync(
                result.ContestId,
                actorId.Value,
                $"Published result {result.Id:N}");
        return Ok(new { result.Id, result.Status });
    }

    [HttpPost("{id:guid}/reopen")]
    [RequirePermission("scoring.correct")]
    [RequirePermission("results.approve")]
    public async Task<IActionResult> ReopenForCorrection(
        Guid id,
        [FromBody] ReopenResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { Message = "A reopening reason is required." });
        var current = await AuthorizedResultAsync(id);
        if (current.Result is null) return current.Error!;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var contest = await _contestsService.GetContestByIdAsync(current.Result.ContestId);
        if (contest is null) return NotFound();
        if (contest.Status != ContestStatus.Completed
            && !contest.Status.IsLegacyResultWorkflowState())
        {
            return Conflict(new
            {
                Message = $"Contest in {contest.Status} has not ended and cannot be reopened."
            });
        }

        var results = await _resultService.GetResultsByContestAsync(contest.Id);
        var eligible = results.Where(item => IsApprovedResultStatus(item.Status))
            .ToArray();
        if (eligible.Length == 0)
            return Conflict(new { Message = "No approved results are available to reopen." });

        foreach (var candidate in eligible)
        {
            var originalStatus = candidate.Status;
            var reopened = await _resultService.ReopenForCorrectionAsync(
                candidate.Id, request.Reason.Trim());
            await RecordWorkflowAuditAsync(
                reopened,
                actorId.Value,
                "ResultReopenedForCorrection",
                originalStatus,
                reopened.Status,
                request.Reason.Trim());
        }
        await _derivedState.RebuildAsync(
            contest.Id,
            actorId.Value,
            $"Reopened results for correction: {request.Reason.Trim()}");

        return Ok(new
        {
            ContestId = contest.Id,
            LifecycleStatus = contest.Status.ToCanonical().ToApiCode(),
            LegacyContestStatus = contest.Status,
            ResultStatus = ContestResultStatus.Corrected,
            ReopenedResults = eligible.Length,
            Reason = request.Reason.Trim(),
            ReopenedBy = actorId.Value
        });
    }

    [HttpPost("{id:guid}/personal-best")]
    public async Task<IActionResult> MarkAsPersonalBest(Guid id)
    {
        var result = await _resultService.MarkAsPersonalBestAsync(id);
        return Ok(new { result.Id, result.IsPersonalBest });
    }

    [HttpPost("{id:guid}/season-best")]
    public async Task<IActionResult> MarkAsSeasonBest(Guid id)
    {
        var result = await _resultService.MarkAsSeasonBestAsync(id);
        return Ok(new { result.Id, result.IsSeasonBest });
    }

    [HttpPost("{id:guid}/competition-record")]
    public async Task<IActionResult> MarkAsCompetitionRecord(Guid id)
    {
        var result = await _resultService.MarkAsCompetitionRecordAsync(id);
        return Ok(new { result.Id, result.IsCompetitionRecord });
    }

    private async Task<(ContestResult? Result, IActionResult? Error)> AuthorizedResultAsync(
        Guid resultId)
    {
        var result = await _resultService.GetResultByIdAsync(resultId);
        if (result is null) return (null, NotFound());
        var contest = await _contestsService.GetContestByIdAsync(result.ContestId);
        if (contest is null) return (null, NotFound());
        return User.CanAccessTenant(contest.TenantId)
            ? (result, null)
            : (null, Forbid());
    }

    private async Task RecordWorkflowAuditAsync(
        ContestResult result,
        Guid actorId,
        string action,
        ContestResultStatus originalStatus,
        ContestResultStatus newStatus,
        string? reason)
    {
        var tenantId = await _db.Contests
            .Where(item => item.Id == result.ContestId)
            .Select(item => item.TenantId)
            .SingleAsync();
        var audit = new AuditEntry(
            Guid.NewGuid(),
            tenantId,
            actorId,
            action,
            nameof(ContestResult),
            result.Id.ToString(),
            JsonSerializer.Serialize(new { Status = originalStatus.ToString() }),
            JsonSerializer.Serialize(new { Status = newStatus.ToString() }),
            reason);
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _db.AuditEntries.Add(audit);
        await _db.SaveChangesAsync();
    }

    private async Task RebuildStandingsAsync(Guid contestId)
    {
        var contest = await _db.Contests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == contestId);
        if (contest?.StageId is null) return;

        var competition = await _db.Competitions
            .AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(item => item.Stages)
            .FirstOrDefaultAsync(item => item.Editions.Any(
                edition => edition.Stages.Any(
                    stage => stage.Id == contest.StageId.Value)));
        if (competition is null) return;
        var stageIds = competition.Editions
            .SelectMany(item => item.Stages)
            .Where(item => item.Type is StageType.League or StageType.Group or StageType.GroupAndKnockout)
            .Select(item => item.Id)
            .ToArray();
        var eligibleContests = await _db.Contests
            .AsNoTracking()
            .Where(item => item.StageId.HasValue && stageIds.Contains(item.StageId.Value))
            .Select(item => new { item.Id, item.GroupId })
            .ToArrayAsync();
        var contestIds = eligibleContests.Select(item => item.Id).ToArray();
        var groupByContest = eligibleContests.ToDictionary(
            item => item.Id,
            item => item.GroupId ?? Guid.Empty);
        var results = await _db.ContestResults
            .AsNoTracking()
            .Where(item =>
                contestIds.Contains(item.ContestId) &&
                (item.Status == ContestResultStatus.Approved ||
                 item.Status == ContestResultStatus.Published ||
                 item.Status == ContestResultStatus.Official))
            .ToListAsync();
        var rugbyCompetition = competition.SportId == RugbySportId;
        var rugbyVariant = RugbyVariants.Fifteens;
        if (rugbyCompetition && competition.RulesetVersionId.HasValue)
        {
            var rugbyValidation = await _db.Sports
                .AsNoTracking()
                .SelectMany(item => item.Versions)
                .Where(item => item.Id == competition.RulesetVersionId.Value)
                .Select(item => item.ValidationConfiguration)
                .SingleOrDefaultAsync();
            rugbyVariant = RugbyVariants.FromValidationConfiguration(rugbyValidation)
                ?? RugbyVariants.Fifteens;
        }
        var rugbySevens = rugbyCompetition && rugbyVariant == RugbyVariants.Sevens;
        var rugbyTries = rugbyCompetition
            ? await _db.LiveScores
                .AsNoTracking()
                .Where(item => contestIds.Contains(item.ContestId)
                    && item.MetricName == "score"
                    && (item.Value == 5 || item.Value == 7))
                .GroupBy(item => new { item.ContestId, item.ParticipantId })
                .Select(group => new
                {
                    group.Key.ContestId,
                    group.Key.ParticipantId,
                    Tries = group.Count()
                })
                .ToDictionaryAsync(
                    item => (item.ContestId, item.ParticipantId),
                    item => item.Tries)
            : new Dictionary<(Guid ContestId, Guid ParticipantId), int>();

        var stats = new Dictionary<(Guid GroupId, Guid ParticipantId), StandingStats>();
        var completedMatches = new List<CompletedStandingMatch>();
        foreach (var contestResults in results.GroupBy(item => item.ContestId))
        {
            var ranked = contestResults
                .Where(item => item.Rank.HasValue)
                .OrderBy(item => item.Rank)
                .ToArray();
            if (ranked.Length == 0) continue;
            var winningRank = ranked[0].Rank;
            var isDraw = ranked.Length == 2
                ? ranked[0].Score == ranked[1].Score
                : ranked.Count(item => item.Rank == winningRank) > 1;
            if (ranked.Length == 2)
            {
                completedMatches.Add(new CompletedStandingMatch(
                    groupByContest[contestResults.Key],
                    ranked[0].ParticipantId,
                    ranked[1].ParticipantId,
                    decimal.ToInt32(decimal.Truncate(ranked[0].Score ?? 0)),
                    decimal.ToInt32(decimal.Truncate(ranked[1].Score ?? 0))));
            }
            foreach (var result in ranked)
            {
                var standingKey = (groupByContest[result.ContestId], result.ParticipantId);
                var current = stats.GetValueOrDefault(standingKey)
                    ?? new StandingStats();
                current.Played++;
                var pointsFor = decimal.ToInt32(decimal.Truncate(result.Score ?? 0));
                var opponent = ranked.FirstOrDefault(item => item.ParticipantId != result.ParticipantId);
                var pointsAgainst = decimal.ToInt32(decimal.Truncate(opponent?.Score ?? 0));
                current.PointsFor += pointsFor;
                current.PointsAgainst += pointsAgainst;
                if (rugbyCompetition)
                {
                    var tries = rugbyTries.GetValueOrDefault(
                        (result.ContestId, result.ParticipantId));
                    current.Tries += tries;
                    if (!rugbySevens && tries >= 4)
                    {
                        current.Points++;
                        current.TryBonusPoints++;
                    }
                }
                if (isDraw)
                {
                    current.Drawn++;
                    current.Points += rugbyCompetition ? 2 : 1;
                }
                else if (result.Rank == winningRank)
                {
                    current.Won++;
                    current.Points += rugbyCompetition ? (rugbySevens ? 3 : 4) : 3;
                }
                else
                {
                    current.Lost++;
                    if (rugbySevens)
                    {
                        current.Points++;
                    }
                    else if (rugbyCompetition && pointsFor < pointsAgainst
                        && pointsAgainst - pointsFor <= 7)
                    {
                        current.Points++;
                        current.LosingBonusPoints++;
                    }
                }
                stats[standingKey] = current;
            }
        }

        if (rugbyCompetition && !rugbySevens)
        {
            foreach (var current in stats.Values.Where(item => item.Played == 5 && item.Won == 5))
            {
                current.Points += 3;
                current.GrandSlamBonusPoints = 3;
            }
        }

        var existing = await _db.Standings
            .Where(item => item.CompetitionId == competition.Id)
            .ToListAsync();
        _db.Standings.RemoveRange(existing);
        var configuredTieBreakers = competition.Editions
            .SelectMany(item => item.Stages)
            .Where(item => item.Type == StageType.GroupAndKnockout)
            .Select(item => item.QualificationTieBreakers)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var groupStats in stats.GroupBy(item => item.Key.GroupId))
        {
            var candidates = groupStats.Select(item => new StandingRankCandidate(
                    item.Key.ParticipantId,
                    item.Value.Points,
                    item.Value.PointsFor,
                    item.Value.PointsAgainst,
                    item.Value.Tries,
                    item.Value.Won))
                .ToArray();
            var headToHead = rugbyCompetition
                ? new Dictionary<Guid, StandingHeadToHead>()
                : BuildFootballHeadToHead(
                    groupStats.Key,
                    candidates,
                    completedMatches);
            var ordered = configuredTieBreakers is { Length: > 0 } && groupStats.Key != Guid.Empty
                ? StandingRankingRules.Rank(candidates, configuredTieBreakers)
                : StandingRankingRules.Rank(candidates, rugbyCompetition
                    ? StandingRankingProfile.Rugby
                    : StandingRankingProfile.Football,
                    headToHead);
            foreach (var ranked in ordered)
            {
                var item = stats[(groupStats.Key, ranked.ParticipantId)];
                var standing = new Standing(
                    Guid.NewGuid(),
                    competition.Id,
                    groupStats.Key,
                    ranked.ParticipantId);
                standing.UpdateStats(
                    item.Played,
                    item.Won,
                    item.Drawn,
                    item.Lost,
                    item.PointsFor,
                    item.PointsAgainst,
                    item.Points);
                standing.SetPosition(ranked.Position);
                if (rugbyCompetition)
                {
                    standing.SetMetadata(JsonSerializer.Serialize(new
                    {
                        tries = item.Tries,
                        variant = rugbyVariant,
                        tryBonusPoints = item.TryBonusPoints,
                        losingBonusPoints = item.LosingBonusPoints,
                        grandSlamBonusPoints = item.GrandSlamBonusPoints
                    }));
                }
                else
                {
                    var tie = headToHead.GetValueOrDefault(ranked.ParticipantId)
                        ?? new StandingHeadToHead(0, 0, 0);
                    standing.SetMetadata(JsonSerializer.Serialize(new
                    {
                        goalsScored = item.PointsFor,
                        goalDifference = item.PointsFor - item.PointsAgainst,
                        headToHeadPoints = tie.Points,
                        headToHeadGoalsFor = tie.PointsFor,
                        headToHeadGoalsAgainst = tie.PointsAgainst
                    }));
                }
                _db.Standings.Add(standing);
            }
        }
        await _db.SaveChangesAsync();
        await ApplyGroupQualificationAsync(contest.StageId.Value);
    }

    private async Task ApplyGroupQualificationAsync(Guid stageId)
    {
        var competition = await _db.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .ThenInclude(stage => stage.Groups)
            .SingleOrDefaultAsync(item => item.Editions.Any(edition =>
                edition.Stages.Any(stage => stage.Id == stageId)));
        if (competition is null) return;
        var groupStage = competition.Editions.SelectMany(item => item.Stages)
            .Single(item => item.Id == stageId);
        if (groupStage.Type != StageType.GroupAndKnockout
            || groupStage.QualificationAppliedAt.HasValue
            || !groupStage.GroupsCount.HasValue
            || !groupStage.TeamsPerGroup.HasValue
            || !groupStage.QualifiersPerGroup.HasValue)
            return;

        var groupContests = await _db.Contests
            .Where(item => item.StageId == groupStage.Id && item.GroupId.HasValue)
            .ToArrayAsync();
        var expectedPerGroup = groupStage.TeamsPerGroup.Value
            * (groupStage.TeamsPerGroup.Value - 1) / 2;
        if (groupContests.Length != expectedPerGroup * groupStage.GroupsCount.Value)
            return;

        var approvedGroupContestIds = await GetApprovedContestIdsAsync(
            groupContests.Select(item => item.Id));
        if (approvedGroupContestIds.Count != groupContests.Length)
            return;

        var standings = await _db.Standings
            .Where(item => item.CompetitionId == competition.Id && item.GroupId != Guid.Empty)
            .ToArrayAsync();
        var orderedGroups = groupStage.Groups.OrderBy(item => item.Name).ToArray();
        if (orderedGroups.Length != groupStage.GroupsCount.Value
            || orderedGroups.Any(group => standings.Count(item => item.GroupId == group.Id)
                != groupStage.TeamsPerGroup.Value))
            return;

        var tieBreakers = (groupStage.QualificationTieBreakers ?? "points,pointDifferential,pointsFor")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var directQualifiers = new List<Standing>();
        for (var position = 0; position < groupStage.QualifiersPerGroup.Value; position++)
        {
            foreach (var group in orderedGroups)
            {
                var groupStandings = OrderQualificationStandings(
                    standings.Where(item => item.GroupId == group.Id), tieBreakers).ToArray();
                directQualifiers.Add(groupStandings[position]);
            }
        }
        var bestRunnerUps = orderedGroups
            .Select(group => OrderQualificationStandings(
                    standings.Where(item => item.GroupId == group.Id), tieBreakers)
                .Skip(groupStage.QualifiersPerGroup.Value)
                .First())
            .OrderBy(item => item, QualificationStandingComparer(tieBreakers))
            .Take(groupStage.BestRunnerUpCount)
            .ToArray();
        var qualifiers = directQualifiers.Concat(bestRunnerUps).ToArray();

        var bracket = await _db.Brackets
            .Include(item => item.Rounds)
            .ThenInclude(round => round.Matches)
            .SingleOrDefaultAsync(item => item.CompetitionId == competition.Id);
        if (bracket is null) return;
        var allMatches = bracket.Rounds.SelectMany(item => item.Matches)
            .ToDictionary(item => item.Id);
        var firstRound = bracket.Rounds.OrderBy(item => item.RoundNumber).First()
            .Matches.OrderBy(item => item.MatchNumber).ToArray();
        var contestIds = allMatches.Values.Where(item => item.ContestId.HasValue)
            .Select(item => item.ContestId!.Value).ToArray();
        var contests = await _db.Contests
            .Include(item => item.Participants)
            .Where(item => contestIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);
        var participantIds = qualifiers.Select(item => item.ParticipantId).ToArray();
        var participants = await _db.Participants
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        for (var index = 0; index < firstRound.Length; index++)
        {
            var first = qualifiers.ElementAtOrDefault(index);
            var second = qualifiers.ElementAtOrDefault(bracket.Size - index - 1);
            var match = firstRound[index];
            match.SetParticipants(first?.ParticipantId, second?.ParticipantId);
            var contest = contests[match.ContestId!.Value];
            if (first is not null)
            {
                var participant = contest.AddParticipant(
                    first.ParticipantId,
                    participants[first.ParticipantId].Type.ToString(),
                    1);
                _db.Entry(participant).State = EntityState.Added;
            }
            if (second is not null)
            {
                var participant = contest.AddParticipant(
                    second.ParticipantId,
                    participants[second.ParticipantId].Type.ToString(),
                    2);
                _db.Entry(participant).State = EntityState.Added;
            }
            if (first is not null && second is not null)
                contest.SetName(
                    $"{participants[first.ParticipantId].Name} vs {participants[second.ParticipantId].Name}");

            var byeWinner = first?.ParticipantId ?? second?.ParticipantId;
            if (!byeWinner.HasValue || (first is not null && second is not null)) continue;
            match.SetWinner(byeWinner.Value);
            contest.Cancel();
            if (!match.NextMatchId.HasValue || !match.NextMatchSlot.HasValue) continue;
            var target = allMatches[match.NextMatchId.Value];
            target.SetParticipants(
                match.NextMatchSlot == 1 ? byeWinner : target.Participant1Id,
                match.NextMatchSlot == 2 ? byeWinner : target.Participant2Id);
            var advancedParticipant = contests[target.ContestId!.Value].AddParticipant(
                byeWinner.Value,
                participants[byeWinner.Value].Type.ToString(),
                match.NextMatchSlot.Value);
            _db.Entry(advancedParticipant).State = EntityState.Added;
        }

        groupStage.MarkQualificationApplied();
        await _db.SaveChangesAsync();
    }

    private static IOrderedEnumerable<Standing> OrderQualificationStandings(
        IEnumerable<Standing> standings,
        IReadOnlyList<string> tieBreakers) =>
        standings.OrderBy(item => item, QualificationStandingComparer(tieBreakers));

    private static IComparer<Standing> QualificationStandingComparer(
        IReadOnlyList<string> tieBreakers) => Comparer<Standing>.Create((left, right) =>
        {
            foreach (var rule in tieBreakers)
            {
                var comparison = rule.ToLowerInvariant() switch
                {
                    "points" => right.Points.CompareTo(left.Points),
                    "pointdifferential" => right.PointDifferential.CompareTo(left.PointDifferential),
                    "pointsfor" => right.PointsFor.CompareTo(left.PointsFor),
                    "wins" => right.Won.CompareTo(left.Won),
                    _ => 0
                };
                if (comparison != 0) return comparison;
            }
            return left.ParticipantId.CompareTo(right.ParticipantId);
        });

    private async Task<bool> AdvanceBracketAsync(Contest contest, Guid winnerId)
    {
        var bracketMatch = await _db.BracketMatches.SingleOrDefaultAsync(match =>
            match.ContestId == contest.Id
            || (contest.SeriesId.HasValue
                && match.SeriesId == contest.SeriesId));
        if (bracketMatch is null || bracketMatch.WinnerId.HasValue) return false;

        var advancingWinnerId = winnerId;
        if (contest.SeriesId.HasValue)
        {
            var seriesGames = await _db.Contests
                .Where(game => game.SeriesId == contest.SeriesId)
                .OrderBy(game => game.SeriesGameNumber)
                .ToListAsync();
            var gameIds = seriesGames.Select(game => game.Id).ToArray();
            var approvedSeriesGameIds = await GetApprovedContestIdsAsync(gameIds);
            approvedSeriesGameIds.UnionWith(seriesGames
                .Where(game => game.Status is ContestStatus.Approved
                    or ContestStatus.Published)
                .Select(game => game.Id));
            if (bracketMatch.IsAggregateTie)
            {
                if (seriesGames.Count != 2 || approvedSeriesGameIds.Count != 2)
                    return false;

                var aggregate = await _db.ContestResults
                    .Where(result => gameIds.Contains(result.ContestId)
                        && result.Score.HasValue
                        && (result.Status == ContestResultStatus.Approved
                            || result.Status == ContestResultStatus.Published
                            || result.Status == ContestResultStatus.Official))
                    .GroupBy(result => result.ParticipantId)
                    .Select(group => new
                    {
                        ParticipantId = group.Key,
                        Score = group.Sum(result => result.Score!.Value)
                    })
                    .OrderByDescending(item => item.Score)
                    .ToArrayAsync();
                if (aggregate.Length < 2 || aggregate[0].Score == aggregate[1].Score)
                    return false; // A tied aggregate requires an approved manual override.

                advancingWinnerId = aggregate[0].ParticipantId;
                bracketMatch.SetScore(
                    aggregate.FirstOrDefault(item => item.ParticipantId == bracketMatch.Participant1Id)?.Score.ToString(),
                    aggregate.FirstOrDefault(item => item.ParticipantId == bracketMatch.Participant2Id)?.Score.ToString());
            }
            else
            {
                var requiredWins = ((contest.SeriesBestOf ?? bracketMatch.BestOf) / 2) + 1;
                var seriesLeader = seriesGames
                    .Where(game => game.WinnerId.HasValue
                        && approvedSeriesGameIds.Contains(game.Id))
                    .GroupBy(game => game.WinnerId!.Value)
                    .Select(group => new
                    {
                        ParticipantId = group.Key,
                        Wins = group.Count()
                    })
                    .OrderByDescending(item => item.Wins)
                    .FirstOrDefault();
                var seriesWins = seriesGames
                    .Where(game => game.WinnerId.HasValue
                        && approvedSeriesGameIds.Contains(game.Id))
                    .GroupBy(game => game.WinnerId!.Value)
                    .ToDictionary(group => group.Key, group => group.Count());
                bracketMatch.SetScore(
                    bracketMatch.Participant1Id.HasValue
                        ? seriesWins.GetValueOrDefault(bracketMatch.Participant1Id.Value).ToString()
                        : null,
                    bracketMatch.Participant2Id.HasValue
                        ? seriesWins.GetValueOrDefault(bracketMatch.Participant2Id.Value).ToString()
                        : null);
                if (seriesLeader is null || seriesLeader.Wins < requiredWins)
                {
                    await _db.SaveChangesAsync();
                    return false;
                }

                advancingWinnerId = seriesLeader.ParticipantId;
                foreach (var unneededGame in seriesGames.Where(game =>
                             game.WinnerId is null
                             && game.Status is ContestStatus.Draft
                                 or ContestStatus.Scheduled
                                 or ContestStatus.CheckInOpen
                                 or ContestStatus.Ready))
                {
                    unneededGame.Cancel();
                }
            }
        }

        bracketMatch.SetWinner(advancingWinnerId);
        if (bracketMatch.NextMatchId.HasValue)
        {
            await PopulateBracketMatchAsync(
                bracketMatch.NextMatchId.Value,
                bracketMatch.NextMatchSlot!.Value,
                advancingWinnerId,
                contest);
        }
        else if (!bracketMatch.IsThirdPlace)
        {
            var round = await _db.BracketRounds
                .SingleAsync(item => item.Id == bracketMatch.RoundId);
            var bracket = await _db.Brackets
                .SingleAsync(item => item.Id == round.BracketId);
            bracket.Complete(advancingWinnerId);
        }

        var loserId = bracketMatch.Participant1Id == advancingWinnerId
            ? bracketMatch.Participant2Id
            : bracketMatch.Participant1Id;
        if (loserId.HasValue && bracketMatch.LoserNextMatchId.HasValue)
        {
            await PopulateBracketMatchAsync(
                bracketMatch.LoserNextMatchId.Value,
                bracketMatch.LoserNextMatchSlot!.Value,
                loserId.Value,
                contest);
        }

        await _db.SaveChangesAsync();
        return bracketMatch.NextMatchId.HasValue;
    }

    private async Task<HashSet<Guid>> GetApprovedContestIdsAsync(IEnumerable<Guid> contestIds)
    {
        var ids = contestIds.Distinct().ToArray();
        if (ids.Length == 0) return new HashSet<Guid>();

        var statuses = await _db.ContestResults
            .AsNoTracking()
            .Where(item => ids.Contains(item.ContestId))
            .Select(item => new { item.ContestId, item.Status })
            .ToArrayAsync();

        return statuses
            .GroupBy(item => item.ContestId)
            .Where(group => group.Any()
                && group.All(item => IsApprovedResultStatus(item.Status)))
            .Select(group => group.Key)
            .ToHashSet();
    }

    private static bool IsApprovedResultStatus(ContestResultStatus status) => status is
        ContestResultStatus.Approved
        or ContestResultStatus.Published
        or ContestResultStatus.Official;

    private async Task PopulateBracketMatchAsync(
        Guid matchId,
        int slot,
        Guid participantId,
        Contest sourceContest)
    {
        var target = await _db.BracketMatches.SingleAsync(match => match.Id == matchId);
        target.SetParticipants(
            slot == 1 ? participantId : target.Participant1Id,
            slot == 2 ? participantId : target.Participant2Id);
        var contests = await _db.Contests
            .Include(item => item.Participants)
            .Where(item => target.SeriesId.HasValue
                ? item.SeriesId == target.SeriesId
                : item.Id == target.ContestId)
            .ToListAsync();
        foreach (var targetContest in contests.Where(item =>
                     item.Participants.All(participant => participant.ParticipantId != participantId)))
        {
            var contestSlot = target.IsAggregateTie && targetContest.SeriesGameNumber == 2
                ? 3 - slot
                : target.SeriesId.HasValue && !target.IsAggregateTie
                    && targetContest.SeriesHomeParticipantSlot.HasValue
                    ? targetContest.SeriesHomeParticipantSlot.Value == slot ? 1 : 2
                    : slot;
            var participant = targetContest.AddParticipant(
                participantId,
                sourceContest.Participants.FirstOrDefault(item => item.ParticipantId == participantId)
                    ?.ParticipantType ?? "Team",
                contestSlot);
            _db.Entry(participant).State = EntityState.Added;
        }
    }

    private sealed class StandingStats
    {
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int PointsFor { get; set; }
        public int PointsAgainst { get; set; }
        public int Tries { get; set; }
        public int TryBonusPoints { get; set; }
        public int LosingBonusPoints { get; set; }
        public int GrandSlamBonusPoints { get; set; }
        public decimal Points { get; set; }
    }

    private static Dictionary<Guid, StandingHeadToHead> BuildFootballHeadToHead(
        Guid groupId,
        IReadOnlyCollection<StandingRankCandidate> candidates,
        IReadOnlyCollection<CompletedStandingMatch> matches)
    {
        var output = candidates.ToDictionary(
            candidate => candidate.ParticipantId,
            _ => new StandingHeadToHead(0, 0, 0));
        var tiedCohorts = candidates.GroupBy(candidate => new
        {
            candidate.Points,
            GoalDifference = candidate.PointsFor - candidate.PointsAgainst,
            GoalsScored = candidate.PointsFor
        }).Where(cohort => cohort.Count() > 1);
        foreach (var cohort in tiedCohorts)
        {
            var participantIds = cohort.Select(candidate => candidate.ParticipantId)
                .ToHashSet();
            foreach (var match in matches.Where(match => match.GroupId == groupId
                && participantIds.Contains(match.Participant1Id)
                && participantIds.Contains(match.Participant2Id)))
            {
                var firstPoints = match.Score1 == match.Score2
                    ? 1
                    : match.Score1 > match.Score2 ? 3 : 0;
                var secondPoints = match.Score1 == match.Score2
                    ? 1
                    : match.Score2 > match.Score1 ? 3 : 0;
                var first = output[match.Participant1Id];
                var second = output[match.Participant2Id];
                output[match.Participant1Id] = first with
                {
                    Points = first.Points + firstPoints,
                    PointsFor = first.PointsFor + match.Score1,
                    PointsAgainst = first.PointsAgainst + match.Score2
                };
                output[match.Participant2Id] = second with
                {
                    Points = second.Points + secondPoints,
                    PointsFor = second.PointsFor + match.Score2,
                    PointsAgainst = second.PointsAgainst + match.Score1
                };
            }
        }
        return output;
    }

    private sealed record CompletedStandingMatch(
        Guid GroupId,
        Guid Participant1Id,
        Guid Participant2Id,
        int Score1,
        int Score2);
}

public record CreateResultRequest(Guid ContestId, Guid EventId, Guid ParticipantId);
public record UpdateScoreRequest(decimal? Score, string? ScoreDisplay, string? Time);
public record CorrectResultRequest(
    decimal? Score,
    string? ScoreDisplay,
    string? Time,
    int? Rank,
    string? Position,
    string Reason);
public record UpdateRankRequest(int? Rank, string? Position);
public record UpdatePerformanceRequest(string? PerformanceData, string? Statistics);
public record ApproveResultRequest(Guid ApprovedBy);
public record RejectResultRequest(string? Reason);
public record ReopenResultRequest(string Reason);
