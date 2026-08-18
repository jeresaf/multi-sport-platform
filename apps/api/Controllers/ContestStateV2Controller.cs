using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Api.Controllers;

/// <summary>
/// Compatibility-first V2 state projection. It separates the contest lifecycle
/// from result approval/publication while legacy Contest.Status remains readable
/// by existing clients during migration.
/// </summary>
[ApiController]
[Authorize]
public sealed class ContestStateV2Controller : ControllerBase
{
    private readonly SportsPlatformDbContext _database;

    public ContestStateV2Controller(SportsPlatformDbContext database)
    {
        _database = database;
    }

    [HttpGet("api/contests/{id:guid}/state-v2")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetState(
        Guid id,
        CancellationToken cancellationToken)
    {
        var contest = await _database.Contests
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        var resultStatuses = await _database.ContestResults
            .AsNoTracking()
            .Where(item => item.ContestId == id)
            .Select(item => item.Status)
            .ToArrayAsync(cancellationToken);

        var activeSession = await _database.ScoringSessions
            .AsNoTracking()
            .Where(item => item.ContestId == id)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.ActiveSegmentId,
                item.ClockSeconds,
                item.ClockCountsDown,
                item.IsClockRunning,
                item.ClockStartedAt,
                Status = item.Status.ToString()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var canonical = contest.Status.ToCanonical();
        return Ok(new
        {
            contest.Id,
            LegacyStatus = contest.Status.ToString(),
            LifecycleStatus = canonical.ToApiCode(),
            ResultStatus = ContestResultWorkflowProjection.Summarize(resultStatuses),
            Phase = activeSession is null ? null : new
            {
                SegmentId = activeSession.ActiveSegmentId,
                activeSession.ClockSeconds,
                activeSession.ClockCountsDown,
                activeSession.IsClockRunning,
                activeSession.ClockStartedAt,
                SessionStatus = activeSession.Status
            },
            contest.StartTime,
            contest.EndTime,
            contest.Venue
        });
    }

    [HttpGet("api/contests/{id:guid}/timeline")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetTimeline(
        Guid id,
        CancellationToken cancellationToken)
    {
        var contest = await _database.Contests
            .AsNoTracking()
            .Include(item => item.Events)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        return Ok(contest.Events
            .OrderBy(item => item.SequenceNumber)
            .ThenBy(item => item.ServerTimestamp)
            .Select(item => new
            {
                item.Id,
                item.SequenceNumber,
                item.EventType,
                item.ParticipantId,
                item.SecondaryParticipantId,
                item.SegmentId,
                item.MetricValue,
                EventStatus = item.Status.ToString(),
                item.DeviceTimestamp,
                item.ServerTimestamp,
                item.CorrectionReason,
                item.SupersededByEventId
            }));
    }
}
