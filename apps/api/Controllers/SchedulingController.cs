using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Scheduling.Entities;
using System.Text.Json;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/competitions/{competitionId:guid}/schedule")]
[Authorize]
public sealed class SchedulingController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly ICompetitionSchedulingService _scheduling;
    private readonly IAuditService _audit;

    public SchedulingController(
        SportsPlatformDbContext database,
        ICompetitionSchedulingService scheduling,
        IAuditService audit)
    {
        _database = database;
        _scheduling = scheduling;
        _audit = audit;
    }

    [HttpGet("validation")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> Validate(Guid competitionId, CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        return Ok(await _scheduling.ValidateAsync(competitionId, cancellationToken));
    }

    [HttpPut("constraints")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> Configure(
        Guid competitionId,
        [FromBody] ConfigureScheduleConstraintsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        try
        {
            var policy = await _scheduling.ConfigureAsync(
                competitionId,
                request.DefaultDurationMinutes,
                request.MinimumRestMinutes,
                request.VenueTurnaroundMinutes,
                request.WindowStartMinutes,
                request.WindowEndMinutes,
                request.BlackoutDates,
                cancellationToken);
            await AuditAsync(
                "ScheduleConstraintsConfigured", tenantId.Value, competitionId,
                request, "Competition scheduling constraints configured", cancellationToken);
            return Ok(policy);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("overrides")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> Override(
        Guid competitionId,
        [FromBody] OverrideScheduleConflictRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "An override reason is required." });
        var applied = await _scheduling.OverrideAsync(
            competitionId, request.ConflictKey, request.Reason, userId.Value, cancellationToken);
        if (applied)
            await AuditAsync(
                "ScheduleConflictOverridden", tenantId.Value, competitionId,
                request, request.Reason, cancellationToken);
        return applied ? NoContent() : NotFound(new { error = "The active conflict was not found." });
    }

    [HttpPut("contests/{contestId:guid}")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RequestAdjustment(
        Guid competitionId,
        Guid contestId,
        [FromBody] RescheduleContestRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "An adjustment reason is required." });
            if (!await ContestBelongsToCompetitionAsync(competitionId, contestId, cancellationToken))
                return NotFound();
            if (await _database.FixtureAdjustments.AnyAsync(item =>
                    item.ContestId == contestId
                    && item.Status == FixtureAdjustmentStatus.Pending,
                    cancellationToken))
                return Conflict(new { error = "A pending adjustment already exists for this fixture." });

            var adjustment = new FixtureAdjustment(
                Guid.NewGuid(), competitionId, contestId, tenantId.Value,
                request.Venue, request.StartTime, request.DurationMinutes,
                request.Reason, userId.Value);
            _database.FixtureAdjustments.Add(adjustment);
            await _database.SaveChangesAsync(cancellationToken);
            await AuditAsync(
                "FixtureAdjustmentRequested", tenantId.Value, contestId,
                request, request.Reason, cancellationToken, "Contest");
            return Accepted(adjustment);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("adjustments")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetAdjustmentHistory(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        return Ok(await _database.FixtureAdjustments.AsNoTracking()
            .Where(item => item.CompetitionId == competitionId)
            .OrderByDescending(item => item.RequestedAt)
            .ToArrayAsync(cancellationToken));
    }

    [HttpPost("adjustments/{adjustmentId:guid}/approve")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ApproveAdjustment(
        Guid competitionId,
        Guid adjustmentId,
        [FromBody] ReviewFixtureAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var adjustment = await _database.FixtureAdjustments.SingleOrDefaultAsync(item =>
            item.Id == adjustmentId && item.CompetitionId == competitionId, cancellationToken);
        if (adjustment is null) return NotFound();
        try
        {
            adjustment.Approve(userId.Value, request.Notes);
            if (!await _scheduling.RescheduleAsync(
                    competitionId, adjustment.ContestId, adjustment.Venue,
                    adjustment.StartTime, adjustment.DurationMinutes,
                    userId.Value, cancellationToken))
                return NotFound();
            await AuditAsync(
                "FixtureAdjustmentApproved", tenantId.Value, adjustment.ContestId,
                new { adjustment.Id, request.Notes },
                request.Notes ?? adjustment.Reason, cancellationToken, "Contest");
            return Ok(adjustment);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("adjustments/{adjustmentId:guid}/reject")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RejectAdjustment(
        Guid competitionId,
        Guid adjustmentId,
        [FromBody] ReviewFixtureAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await FindTenantIdAsync(competitionId, cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var adjustment = await _database.FixtureAdjustments.SingleOrDefaultAsync(item =>
            item.Id == adjustmentId && item.CompetitionId == competitionId, cancellationToken);
        if (adjustment is null) return NotFound();
        try
        {
            adjustment.Reject(userId.Value, request.Notes);
            await _database.SaveChangesAsync(cancellationToken);
            await AuditAsync(
                "FixtureAdjustmentRejected", tenantId.Value, adjustment.ContestId,
                new { adjustment.Id, request.Notes },
                request.Notes ?? adjustment.Reason, cancellationToken, "Contest");
            return Ok(adjustment);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<bool> ContestBelongsToCompetitionAsync(
        Guid competitionId,
        Guid contestId,
        CancellationToken cancellationToken)
    {
        var stageIds = await _database.Competitions
            .Where(item => item.Id == competitionId)
            .SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return await _database.Contests.AnyAsync(item =>
            item.Id == contestId && item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value), cancellationToken);
    }

    private async Task<Guid?> FindTenantIdAsync(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        return await _database.Competitions
            .Where(item => item.Id == competitionId)
            .Select(item => (Guid?)item.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task AuditAsync(
        string action,
        Guid tenantId,
        Guid entityId,
        object value,
        string reason,
        CancellationToken cancellationToken,
        string entityType = "Competition") => _audit.RecordAsync(
            action,
            entityType,
            entityId.ToString(),
            tenantId,
            User.UserId(),
            null,
            JsonSerializer.Serialize(value),
            reason,
            cancellationToken);
}

public sealed record ConfigureScheduleConstraintsRequest(
    int DefaultDurationMinutes = 120,
    int MinimumRestMinutes = 60,
    int VenueTurnaroundMinutes = 15,
    int WindowStartMinutes = 0,
    int WindowEndMinutes = 1440,
    IReadOnlyCollection<DateOnly>? BlackoutDates = null);

public sealed record OverrideScheduleConflictRequest(string ConflictKey, string Reason);
public sealed record RescheduleContestRequest(
    string Venue,
    DateTime StartTime,
    int DurationMinutes = 120,
    string? Reason = null);
public sealed record ReviewFixtureAdjustmentRequest(string? Notes = null);
