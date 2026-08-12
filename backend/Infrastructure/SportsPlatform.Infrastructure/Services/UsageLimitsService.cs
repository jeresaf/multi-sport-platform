using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.Services;

public sealed record UsageLimitWarning(
    string Metric,
    int Current,
    int Limit,
    int Percentage,
    string Severity,
    bool HardLimitEnforced);

public sealed record TenantUsageSummary(
    Dictionary<string, int> Current,
    Dictionary<string, int> Limits,
    IReadOnlyList<UsageLimitWarning> Warnings,
    bool HardLimitsEnabled);

public interface IUsageLimitsService
{
    Task<Dictionary<string, int>> GetUsageLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetCurrentUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantUsageSummary> GetUsageSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> CanCreateResourceAsync(Guid tenantId, string resourceType, CancellationToken cancellationToken = default);
    Task IncrementUsageAsync(Guid tenantId, string metric, long amount = 1, CancellationToken cancellationToken = default);
    Task SetUsageLimitsAsync(Guid subscriptionId, Dictionary<string, int> limits, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetPlanDefaultLimitsAsync(string plan);
}

public class UsageLimitsService : IUsageLimitsService
{
    public const string HardLimitsFeature = "usage.hard_limits";
    public const string ApiRequestsMetric = "api_requests_per_hour";
    public const string StorageBytesMetric = "storage_bytes";

    private readonly ISubscriptionsService _subscriptionsService;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly SportsPlatformDbContext _context;

    private static readonly Dictionary<string, Dictionary<string, int>> PlanDefaults = new()
    {
        ["free"] = Limits(5, 10, 50, 100, 1, 3, 100, 1_000, 1_000),
        ["starter"] = Limits(15, 50, 200, 500, 3, 10, 500, 5_000, 10_000),
        ["professional"] = Limits(50, 200, 1_000, 5_000, 10, 50, 2_000, 20_000, 100_000),
        ["enterprise"] = Limits(-1, -1, -1, -1, -1, -1, -1, -1, -1),
    };

    public UsageLimitsService(
        ISubscriptionsService subscriptionsService,
        IFeatureEntitlementService featureEntitlements,
        SportsPlatformDbContext context)
    {
        _subscriptionsService = subscriptionsService;
        _featureEntitlements = featureEntitlements;
        _context = context;
    }

    public async Task<Dictionary<string, int>> GetUsageLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionsService.GetActiveSubscriptionAsync(tenantId, cancellationToken);
        if (subscription == null) return new Dictionary<string, int>();

        var result = GetPlanDefaultsSync(subscription.Plan);
        if (string.IsNullOrWhiteSpace(subscription.UsageLimits) || subscription.UsageLimits == "{}") return result;

        try
        {
            var customLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(subscription.UsageLimits);
            if (customLimits != null)
                foreach (var item in customLimits) result[item.Key] = item.Value;
        }
        catch (JsonException)
        {
            // Preserve usable plan defaults if legacy data contains malformed JSON.
        }

        return result;
    }

    public async Task<Dictionary<string, int>> GetCurrentUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var activeCompetitions = _context.Competitions.Where(competition =>
            competition.TenantId == tenantId &&
            competition.Status != CompetitionStatus.Closed &&
            competition.Status != CompetitionStatus.Archived);

        var administratorRoles = new[] { SystemRoles.TenantOwner, SystemRoles.CompetitionAdministrator };
        var scorerRoles = new[] { SystemRoles.Scorer, SystemRoles.HeadOfficial };
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var hourStart = StartOfUtcHour(DateTime.UtcNow);

        var sports = await _context.TenantSports.CountAsync(
            item => item.TenantId == tenantId && item.IsEnabled,
            cancellationToken);
        var competitions = await activeCompetitions.CountAsync(cancellationToken);
        var participants = await _context.Participants.CountAsync(participant => participant.TenantId == tenantId, cancellationToken);
        var administrators = await CountUsersInRolesAsync(tenantId, administratorRoles, cancellationToken);
        var scorers = await CountUsersInRolesAsync(tenantId, scorerRoles, cancellationToken);
        var notifications = await (
            from notification in _context.Notifications
            join user in _context.Users on notification.UserId equals user.Id
            where user.TenantId == tenantId && notification.CreatedAt >= monthStart
            select notification.Id).CountAsync(cancellationToken);
        var apiRequests = await GetCounterAsync(tenantId, ApiRequestsMetric, hourStart, cancellationToken);
        var storageBytes = await GetCounterAsync(tenantId, StorageBytesMetric, DateTime.UnixEpoch, cancellationToken);

        return new Dictionary<string, int>
        {
            ["max_sports"] = sports,
            ["max_competitions"] = competitions,
            ["max_participants"] = participants,
            ["max_admins"] = administrators,
            ["max_operators"] = scorers,
            ["storage_mb"] = ClampToInt((storageBytes + 1_048_575) / 1_048_576),
            ["notifications_per_month"] = notifications,
            [ApiRequestsMetric] = ClampToInt(apiRequests),
        };
    }

    public async Task<TenantUsageSummary> GetUsageSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var limits = await GetUsageLimitsAsync(tenantId, cancellationToken);
        var current = await GetCurrentUsageAsync(tenantId, cancellationToken);
        var hardLimitsEnabled = await _featureEntitlements.HasFeatureAsync(tenantId, HardLimitsFeature, cancellationToken);
        var warnings = new List<UsageLimitWarning>();

        foreach (var (metric, limit) in limits)
        {
            if (limit <= 0) continue;
            var value = current.GetValueOrDefault(metric);
            var percentage = (int)Math.Min(1000L, value * 100L / limit);
            if (percentage < 80) continue;
            warnings.Add(new UsageLimitWarning(
                metric,
                value,
                limit,
                percentage,
                value >= limit ? "exceeded" : "warning",
                hardLimitsEnabled));
        }

        return new TenantUsageSummary(current, limits, warnings, hardLimitsEnabled);
    }

    public async Task<bool> CanCreateResourceAsync(Guid tenantId, string resourceType, CancellationToken cancellationToken = default)
    {
        if (!await _featureEntitlements.HasFeatureAsync(tenantId, HardLimitsFeature, cancellationToken)) return true;

        var limits = await GetUsageLimitsAsync(tenantId, cancellationToken);
        if (!limits.TryGetValue(resourceType, out var limit) || limit == -1) return true;
        var current = await GetCurrentUsageAsync(tenantId, cancellationToken);
        return current.GetValueOrDefault(resourceType) < limit;
    }

    public async Task IncrementUsageAsync(
        Guid tenantId,
        string metric,
        long amount = 1,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(metric) || amount == 0) return;
        var periodStart = metric switch
        {
            ApiRequestsMetric => StartOfUtcHour(DateTime.UtcNow),
            StorageBytesMetric => DateTime.UnixEpoch,
            _ => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TenantUsageCounters"
                    ("Id", "TenantId", "Metric", "PeriodStartUtc", "Value", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {tenantId}, {metric}, {periodStart}, {Math.Max(0, amount)}, {DateTime.UtcNow}, {DateTime.UtcNow})
                ON CONFLICT ("TenantId", "Metric", "PeriodStartUtc")
                DO UPDATE SET
                    "Value" = GREATEST(0, "TenantUsageCounters"."Value" + {amount}),
                    "UpdatedAt" = {DateTime.UtcNow};
                """, cancellationToken);
            return;
        }

        var counter = await _context.TenantUsageCounters.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Metric == metric && item.PeriodStartUtc == periodStart,
            cancellationToken);
        if (counter == null)
            _context.TenantUsageCounters.Add(new TenantUsageCounter(Guid.NewGuid(), tenantId, metric, periodStart, Math.Max(0, amount)));
        else
            counter.Increment(amount);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUsageLimitsAsync(Guid subscriptionId, Dictionary<string, int> limits, CancellationToken cancellationToken = default)
    {
        await _subscriptionsService.UpdateUsageLimitsAsync(subscriptionId, JsonSerializer.Serialize(limits), cancellationToken);
    }

    public Task<Dictionary<string, int>> GetPlanDefaultLimitsAsync(string plan) =>
        Task.FromResult(GetPlanDefaultsSync(plan));

    private async Task<int> CountUsersInRolesAsync(Guid tenantId, string[] roleNames, CancellationToken cancellationToken) =>
        await (
            from user in _context.Users
            join userRole in _context.UserRoles on user.Id equals userRole.UserId
            join role in _context.Roles on userRole.RoleId equals role.Id
            where user.TenantId == tenantId && user.IsActive && !user.IsSuspended && roleNames.Contains(role.Name)
            select user.Id).Distinct().CountAsync(cancellationToken);

    private async Task<long> GetCounterAsync(Guid tenantId, string metric, DateTime periodStart, CancellationToken cancellationToken) =>
        await _context.TenantUsageCounters
            .Where(counter => counter.TenantId == tenantId && counter.Metric == metric && counter.PeriodStartUtc == periodStart)
            .Select(counter => (long?)counter.Value)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

    private Dictionary<string, int> GetPlanDefaultsSync(string plan) =>
        new(PlanDefaults.GetValueOrDefault(plan.ToLowerInvariant(), PlanDefaults["free"]));

    private static DateTime StartOfUtcHour(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);

    private static int ClampToInt(long value) => (int)Math.Min(int.MaxValue, Math.Max(0, value));

    private static Dictionary<string, int> Limits(
        int sports, int competitions, int contests, int participants, int admins,
        int operators, int storageMb, int apiRequests, int notifications) => new()
    {
        ["max_sports"] = sports,
        ["max_competitions"] = competitions,
        ["max_contests_per_competition"] = contests,
        ["max_participants"] = participants,
        ["max_admins"] = admins,
        ["max_operators"] = operators,
        ["storage_mb"] = storageMb,
        [ApiRequestsMetric] = apiRequests,
        ["notifications_per_month"] = notifications,
    };
}
