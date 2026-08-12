using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/platform-admin")]
[Authorize]
public sealed class PlatformAdminController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly ISubscriptionsService _subscriptions;
    private readonly IUsageLimitsService _usage;
    private readonly IFeatureEntitlementService _entitlements;
    private readonly IReportGenerationQueue _reportQueue;

    public PlatformAdminController(
        SportsPlatformDbContext database,
        ISubscriptionsService subscriptions,
        IUsageLimitsService usage,
        IFeatureEntitlementService entitlements,
        IReportGenerationQueue reportQueue)
    {
        _database = database;
        _subscriptions = subscriptions;
        _usage = usage;
        _entitlements = entitlements;
        _reportQueue = reportQueue;
    }

    [HttpGet("dashboard")]
    [RequirePermission("support.read")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? search,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? historyContestId,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        var now = DateTime.UtcNow;
        var allTenants = await _database.Tenants
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var normalizedSearch = search?.Trim();
        var tenants = allTenants
            .Where(item => !tenantId.HasValue || item.Id == tenantId.Value)
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch)
                || item.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.Slug.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.Id.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var notificationStates = await GetNotificationStatesAsync(
            tenants.Select(item => item.Id), now, cancellationToken);
        var tenantRows = new List<object>(tenants.Length);
        var usageWarningCount = 0;
        foreach (var tenant in tenants)
        {
            var subscription = await _subscriptions.GetActiveSubscriptionAsync(
                tenant.Id, cancellationToken);
            var usage = await _usage.GetUsageSummaryAsync(tenant.Id, cancellationToken);
            var entitlements = await _entitlements.GetFeatureFlagsAsync(
                tenant.Id, cancellationToken);
            usageWarningCount += usage.Warnings.Count;
            tenantRows.Add(new
            {
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                status = tenant.Status.ToString(),
                lease = subscription is null ? null : new
                {
                    subscription.Id,
                    subscription.Plan,
                    status = subscription.Status.ToString(),
                    subscription.StartDate,
                    subscription.EndDate,
                    subscription.GracePeriodEnd,
                    daysRemaining = (int)Math.Ceiling((subscription.EndDate - now).TotalDays)
                },
                usage,
                entitlements,
                enabledEntitlementCount = entitlements.Count(item => item.Value),
                notifications = notificationStates.TryGetValue(tenant.Id, out var notificationState)
                    ? notificationState
                    : new NotificationSupportState(false, null, null, null)
            });
        }

        var failedJobsQuery = _database.Reports.AsNoTracking()
            .Where(item => item.Status == ReportStatus.Failed);
        var syncConflictsQuery = _database.AuditEntries.AsNoTracking()
            .Where(item => item.Action == "LiveScoreSyncConflict");
        var filteredTenantIds = tenants.Select(item => item.Id).ToArray();
        if (tenantId.HasValue)
        {
            failedJobsQuery = failedJobsQuery.Where(item => item.TenantId == tenantId.Value);
            syncConflictsQuery = syncConflictsQuery.Where(item => item.TenantId == tenantId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            failedJobsQuery = failedJobsQuery.Where(item => filteredTenantIds.Contains(item.TenantId));
            syncConflictsQuery = syncConflictsQuery.Where(item => item.TenantId.HasValue
                && filteredTenantIds.Contains(item.TenantId.Value));
        }
        var failedJobs = await failedJobsQuery
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(50)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                item.Name,
                type = item.Type.ToString(),
                format = item.Format.ToString(),
                status = item.Status.ToString(),
                item.ErrorMessage,
                item.CreatedAt,
                item.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
        var syncConflicts = await syncConflictsQuery
            .OrderByDescending(item => item.Timestamp)
            .Take(50)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                contestId = item.EntityId,
                details = item.NewValue,
                item.Reason,
                item.Timestamp
            })
            .ToArrayAsync(cancellationToken);
        var eventHistory = await GetContestEventHistoryAsync(historyContestId, cancellationToken);

        var recentAudit = await _database.AuditEntries
            .AsNoTracking()
            .OrderByDescending(item => item.Timestamp)
            .Take(30)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                item.UserId,
                item.Action,
                item.EntityType,
                item.EntityId,
                item.Reason,
                item.Timestamp
            })
            .ToArrayAsync(cancellationToken);
        var databaseHealthy = await _database.Database.CanConnectAsync(cancellationToken);
        var activeTenantCount = allTenants.Count(item => item.Status.ToString() == "Active");
        var activeCompetitions = await _database.Competitions.AsNoTracking()
            .CountAsync(item => item.Status != CompetitionStatus.Closed
                && item.Status != CompetitionStatus.Archived, cancellationToken);
        var liveContests = await _database.Contests.AsNoTracking()
            .CountAsync(item => item.Status == ContestStatus.InProgress
                || item.Status == ContestStatus.Paused, cancellationToken);
        var scheduledNextSevenDays = await _database.Contests.AsNoTracking()
            .CountAsync(item => item.StartTime >= now
                && item.StartTime < now.AddDays(7), cancellationToken);

        return Ok(new
        {
            generatedAtUtc = now,
            summary = new
            {
                totalTenants = allTenants.Length,
                activeTenants = activeTenantCount,
                activeCompetitions,
                liveContests,
                scheduledNextSevenDays,
                usageWarnings = usageWarningCount
            },
            health = new
            {
                status = databaseHealthy ? "Healthy" : "Degraded",
                database = databaseHealthy ? "Connected" : "Unavailable",
                checkedAtUtc = now
            },
            tenants = tenantRows,
            failedJobs,
            syncConflicts,
            eventHistory,
            recentAudit
        });
    }

    [HttpPost("tenants/{tenantId:guid}/support-action")]
    [RequirePermission("support.manage")]
    public async Task<IActionResult> ApplySupportAction(
        Guid tenantId,
        [FromBody] PlatformSupportActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        if (!request.Confirmed)
            return BadRequest(new { error = "Explicit confirmation is required." });
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A support action reason is required." });

        var tenant = await _database.Tenants.SingleOrDefaultAsync(
            item => item.Id == tenantId, cancellationToken);
        if (tenant is null) return NotFound();
        var originalStatus = tenant.Status.ToString();
        var action = request.Action.Trim().ToLowerInvariant();
        switch (action)
        {
            case "activate":
                tenant.Activate();
                break;
            case "suspend":
                tenant.Suspend();
                break;
            default:
                return BadRequest(new { error = "Support action must be Activate or Suspend." });
        }

        var audit = new AuditEntry(
            Guid.NewGuid(),
            tenant.Id,
            User.UserId(),
            action == "activate" ? "PlatformSupportActivatedTenant" : "PlatformSupportSuspendedTenant",
            "Tenant",
            tenant.Id.ToString(),
            JsonSerializer.Serialize(new { status = originalStatus }),
            JsonSerializer.Serialize(new { status = tenant.Status.ToString() }),
            request.Reason.Trim());
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _database.AuditEntries.Add(audit);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            status = tenant.Status.ToString(),
            auditId = audit.Id
        });
    }

    [HttpPost("jobs/{reportId:guid}/reprocess")]
    [RequirePermission("support.manage")]
    public async Task<IActionResult> ReprocessFailedJob(
        Guid reportId,
        [FromBody] PlatformConfirmedReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var validation = ValidateConfirmedReason(request.Confirmed, request.Reason);
        if (validation is not null) return validation;

        var report = await _database.Reports.SingleOrDefaultAsync(
            item => item.Id == reportId, cancellationToken);
        if (report is null) return NotFound();
        if (report.Status != ReportStatus.Failed)
            return Conflict(new { error = "Only failed report jobs can be safely reprocessed." });

        var previousError = report.ErrorMessage;
        report.ResetForGeneration();
        var audit = CreateAudit(
            report.TenantId,
            "PlatformSupportReprocessedReportJob",
            "Report",
            report.Id.ToString(),
            new { status = ReportStatus.Failed.ToString(), error = previousError },
            new { status = report.Status.ToString() },
            request.Reason);
        _database.AuditEntries.Add(audit);
        await _database.SaveChangesAsync(cancellationToken);
        await _reportQueue.EnqueueAsync(report.Id, cancellationToken);

        return Accepted(new
        {
            report.Id,
            report.TenantId,
            status = report.Status.ToString(),
            auditId = audit.Id
        });
    }

    [HttpPost("tenants/{tenantId:guid}/notifications")]
    [RequirePermission("support.manage")]
    public async Task<IActionResult> SetTenantNotificationDelivery(
        Guid tenantId,
        [FromBody] PlatformNotificationControlRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var validation = ValidateConfirmedReason(request.Confirmed, request.Reason);
        if (validation is not null) return validation;
        if (!await _database.Tenants.AsNoTracking()
                .AnyAsync(item => item.Id == tenantId, cancellationToken))
            return NotFound();

        DateTime? disabledUntilUtc = null;
        if (request.Disabled)
        {
            disabledUntilUtc = request.DisabledUntilUtc?.ToUniversalTime();
            if (!disabledUntilUtc.HasValue || disabledUntilUtc <= DateTime.UtcNow)
                return BadRequest(new { error = "A future notification resume time is required." });
            if (disabledUntilUtc > DateTime.UtcNow.AddDays(7))
                return BadRequest(new { error = "Notifications can be paused for at most seven days." });
        }

        var previousStates = await GetNotificationStatesAsync(
            new[] { tenantId }, DateTime.UtcNow, cancellationToken);
        previousStates.TryGetValue(tenantId, out var previousState);
        var audit = CreateAudit(
            tenantId,
            request.Disabled
                ? "PlatformSupportDisabledNotifications"
                : "PlatformSupportEnabledNotifications",
            "TenantNotificationDelivery",
            tenantId.ToString(),
            previousState,
            new { disabled = request.Disabled, disabledUntilUtc },
            request.Reason);
        _database.AuditEntries.Add(audit);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            tenantId,
            disabled = request.Disabled,
            disabledUntilUtc,
            auditId = audit.Id
        });
    }

    private async Task<object?> GetContestEventHistoryAsync(
        Guid? contestId,
        CancellationToken cancellationToken)
    {
        if (!contestId.HasValue) return null;
        var contest = await _database.Contests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == contestId.Value, cancellationToken);
        if (contest is null) return new { contestId, notFound = true, events = Array.Empty<object>() };

        var events = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contest.Id)
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.ServerTimestamp)
            .Select(item => new
            {
                item.Id,
                item.EventId,
                item.EventType,
                item.ParticipantId,
                item.SecondaryParticipantId,
                item.SegmentId,
                item.MetricName,
                item.Value,
                item.DisplayValue,
                item.Sequence,
                item.DeviceId,
                item.DeviceTimestamp,
                item.ServerTimestamp
            })
            .ToArrayAsync(cancellationToken);
        var eventIds = events.Select(item => item.Id).ToArray();
        var updates = await _database.LiveScoreUpdates.AsNoTracking()
            .Where(item => eventIds.Contains(item.LiveScoreId))
            .OrderBy(item => item.UpdatedAt)
            .Select(item => new
            {
                item.Id,
                item.LiveScoreId,
                item.UpdateType,
                item.OldValue,
                item.NewValue,
                item.UpdatedBy,
                item.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
        return new
        {
            contest.Id,
            contest.TenantId,
            contest.Name,
            status = contest.Status.ToString(),
            events,
            updates
        };
    }

    private async Task<Dictionary<Guid, NotificationSupportState>> GetNotificationStatesAsync(
        IEnumerable<Guid> tenantIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ids = tenantIds.Distinct().ToArray();
        if (ids.Length == 0) return new();
        var records = await _database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId.HasValue
                && ids.Contains(item.TenantId.Value)
                && (item.Action == "PlatformSupportDisabledNotifications"
                    || item.Action == "PlatformSupportEnabledNotifications"))
            .OrderByDescending(item => item.Timestamp)
            .ToArrayAsync(cancellationToken);
        return records
            .GroupBy(item => item.TenantId!.Value)
            .Select(group => group.First())
            .ToDictionary(
                item => item.TenantId!.Value,
                item => ParseNotificationState(item, now));
    }

    private static NotificationSupportState ParseNotificationState(AuditEntry entry, DateTime now)
    {
        DateTime? until = null;
        if (entry.Action == "PlatformSupportDisabledNotifications"
            && !string.IsNullOrWhiteSpace(entry.NewValue))
        {
            using var document = JsonDocument.Parse(entry.NewValue);
            if (document.RootElement.TryGetProperty("disabledUntilUtc", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.TryGetDateTime(out var parsed))
                until = parsed.ToUniversalTime();
        }
        return new NotificationSupportState(
            until.HasValue && until > now,
            until,
            entry.Reason,
            entry.Timestamp);
    }

    private AuditEntry CreateAudit(
        Guid tenantId,
        string action,
        string entityType,
        string entityId,
        object? oldValue,
        object? newValue,
        string reason)
    {
        var audit = new AuditEntry(
            Guid.NewGuid(),
            tenantId,
            User.UserId(),
            action,
            entityType,
            entityId,
            oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            newValue is null ? null : JsonSerializer.Serialize(newValue),
            reason.Trim());
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        return audit;
    }

    private IActionResult? ValidateConfirmedReason(bool confirmed, string? reason)
    {
        if (!confirmed) return BadRequest(new { error = "Explicit confirmation is required." });
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { error = "A support action reason is required." });
        if (reason.Trim().Length > 1000)
            return BadRequest(new { error = "The support reason must be 1000 characters or fewer." });
        return null;
    }
}

public sealed record PlatformSupportActionRequest(
    string Action,
    string Reason,
    bool Confirmed);

public sealed record PlatformConfirmedReasonRequest(string Reason, bool Confirmed);

public sealed record PlatformNotificationControlRequest(
    bool Disabled,
    DateTime? DisabledUntilUtc,
    string Reason,
    bool Confirmed);

public sealed record NotificationSupportState(
    bool Disabled,
    DateTime? DisabledUntilUtc,
    string? Reason,
    DateTime? ChangedAtUtc);
