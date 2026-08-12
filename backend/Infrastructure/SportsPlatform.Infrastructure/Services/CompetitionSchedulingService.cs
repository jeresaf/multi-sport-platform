using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Notifications.Services;
using SportsPlatform.Modules.Scheduling.Entities;
using SportsPlatform.Modules.Scheduling.Services;

namespace SportsPlatform.Infrastructure.Services;

public sealed record ScheduleConflictView(
    string Key,
    string Type,
    string Message,
    Guid ContestId,
    Guid? ConflictingContestId,
    bool IsOverridden,
    string? OverrideReason);

public sealed record ScheduledContestView(
    Guid Id,
    string Name,
    string? Venue,
    DateTime? StartTime,
    int DurationMinutes);

public sealed record CompetitionScheduleValidation(
    Guid CompetitionId,
    bool CanPublish,
    IReadOnlyList<ScheduleConflictView> Conflicts,
    IReadOnlyList<ScheduledContestView> Contests,
    CompetitionSchedulePolicy Policy);

public interface ICompetitionSchedulingService
{
    Task<CompetitionScheduleValidation?> ValidateAsync(Guid competitionId, CancellationToken cancellationToken = default);
    Task<CompetitionSchedulePolicy?> ConfigureAsync(Guid competitionId, int defaultDurationMinutes,
        int minimumRestMinutes, int venueTurnaroundMinutes, int windowStartMinutes,
        int windowEndMinutes, IEnumerable<DateOnly>? blackoutDates, CancellationToken cancellationToken = default);
    Task<bool> OverrideAsync(Guid competitionId, string conflictKey, string reason,
        Guid requestedBy, CancellationToken cancellationToken = default);
    Task<bool> RescheduleAsync(Guid competitionId, Guid contestId, string venue,
        DateTime startTime, int durationMinutes, Guid requestedBy,
        CancellationToken cancellationToken = default);
}

public sealed class CompetitionSchedulingService : ICompetitionSchedulingService
{
    private readonly SportsPlatformDbContext _database;
    private readonly CompetitionScheduleConflictDetector _detector;
    private readonly IPushDeliveryClient _pushDelivery;

    public CompetitionSchedulingService(
        SportsPlatformDbContext database,
        CompetitionScheduleConflictDetector detector,
        IPushDeliveryClient pushDelivery)
    {
        _database = database;
        _detector = detector;
        _pushDelivery = pushDelivery;
    }

    public async Task<CompetitionScheduleValidation?> ValidateAsync(
        Guid competitionId,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await LoadCompetitionAsync(competitionId, cancellationToken);
        if (aggregate is null) return null;
        var (competition, contests) = aggregate.Value;
        var policy = await _database.CompetitionSchedulePolicies
            .SingleOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken)
            ?? new CompetitionSchedulePolicy(Guid.NewGuid(), competitionId, competition.TenantId);
        var overrides = await _database.ScheduleConflictOverrides
            .Where(item => item.CompetitionId == competitionId)
            .ToDictionaryAsync(item => item.ConflictKey, cancellationToken);
        var conflicts = _detector.Detect(contests.Select(item => ToScheduleEvent(item, policy)), policy)
            .Select(item => new ScheduleConflictView(
                item.Key,
                item.Type,
                item.Message,
                item.ContestId,
                item.ConflictingContestId,
                overrides.ContainsKey(item.Key),
                overrides.GetValueOrDefault(item.Key)?.Reason))
            .ToArray();

        return new CompetitionScheduleValidation(
            competitionId,
            conflicts.All(item => item.IsOverridden),
            conflicts,
            contests.OrderBy(item => item.StartTime).ThenBy(item => item.SortOrder)
                .Select(item => new ScheduledContestView(
                    item.Id,
                    item.Name ?? $"Contest {item.SortOrder}",
                    item.Venue,
                    item.StartTime,
                    item.ScheduledDurationMinutes > 0
                        ? item.ScheduledDurationMinutes
                        : policy.DefaultDurationMinutes))
                .ToArray(),
            policy);
    }

    public async Task<CompetitionSchedulePolicy?> ConfigureAsync(
        Guid competitionId,
        int defaultDurationMinutes,
        int minimumRestMinutes,
        int venueTurnaroundMinutes,
        int windowStartMinutes,
        int windowEndMinutes,
        IEnumerable<DateOnly>? blackoutDates,
        CancellationToken cancellationToken = default)
    {
        var competition = await _database.Competitions.FindAsync(
            new object[] { competitionId }, cancellationToken);
        if (competition is null) return null;
        var policy = await _database.CompetitionSchedulePolicies
            .SingleOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
        if (policy is null)
        {
            policy = new CompetitionSchedulePolicy(Guid.NewGuid(), competitionId, competition.TenantId);
            _database.CompetitionSchedulePolicies.Add(policy);
        }
        policy.Configure(defaultDurationMinutes, minimumRestMinutes, venueTurnaroundMinutes,
            windowStartMinutes, windowEndMinutes, blackoutDates);
        await RemoveOverridesAsync(competitionId, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<bool> OverrideAsync(
        Guid competitionId,
        string conflictKey,
        string reason,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(competitionId, cancellationToken);
        if (validation is null || validation.Conflicts.All(item => item.Key != conflictKey))
            return false;
        var competition = await _database.Competitions.FindAsync(
            new object[] { competitionId }, cancellationToken);
        if (competition is null) return false;
        var existing = await _database.ScheduleConflictOverrides.SingleOrDefaultAsync(
            item => item.CompetitionId == competitionId && item.ConflictKey == conflictKey,
            cancellationToken);
        if (existing is null)
        {
            _database.ScheduleConflictOverrides.Add(new ScheduleConflictOverride(
                Guid.NewGuid(), competitionId, competition.TenantId,
                conflictKey, reason, requestedBy));
        }
        else
        {
            existing.Revise(reason, requestedBy);
        }
        await _database.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RescheduleAsync(
        Guid competitionId,
        Guid contestId,
        string venue,
        DateTime startTime,
        int durationMinutes,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await LoadCompetitionAsync(competitionId, cancellationToken);
        if (aggregate is null) return false;
        var (competition, contests) = aggregate.Value;
        var contest = contests.SingleOrDefault(item => item.Id == contestId);
        if (contest is null) return false;
        var previousVenue = contest.Venue;
        var previousStart = contest.StartTime;
        contest.Schedule(venue, startTime, durationMinutes);
        await RemoveOverridesAsync(competitionId, cancellationToken);

        var metadata = JsonSerializer.Serialize(new
        {
            competitionId,
            contestId,
            previousVenue,
            previousStart,
            venue,
            startTime,
            durationMinutes
        });
        var recipients = await _database.Users
            .Where(item => item.TenantId == competition.TenantId && item.IsActive && !item.IsSuspended)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (!recipients.Contains(requestedBy)) recipients.Add(requestedBy);
        foreach (var userId in recipients.Distinct())
        {
            var notification = new Notification(
                Guid.NewGuid(), userId, NotificationType.Contest, NotificationChannel.InApp,
                "Contest schedule changed",
                $"{contest.Name ?? "Contest"} is now scheduled for {startTime:u} at {venue}.");
            notification.SetMetadata(metadata);
            _database.Notifications.Add(notification);
        }
        await _database.SaveChangesAsync(cancellationToken);

        if (_pushDelivery.IsConfigured)
        {
            var tenantSlug = await _database.Tenants.AsNoTracking()
                .Where(item => item.Id == competition.TenantId)
                .Select(item => item.Slug)
                .SingleAsync(cancellationToken);
            var type = previousStart.HasValue && startTime > previousStart.Value
                ? PushNotificationTypes.Postponement
                : PushNotificationTypes.Announcement;
            await _pushDelivery.SendToTopicsAsync(
                PushTopics.Contest(tenantSlug, contestId, competitionId),
                new PushDelivery(
                    $"schedule-{contestId:N}-{startTime.ToUniversalTime().Ticks}",
                    type,
                    "Contest schedule changed",
                    $"{contest.Name ?? "Contest"} is now scheduled for {startTime:u} at {venue}.",
                    $"contest:{contestId}",
                    new Dictionary<string, string>
                    {
                        ["contestId"] = contestId.ToString(),
                        ["competitionId"] = competitionId.ToString(),
                        ["tenantSlug"] = tenantSlug
                    }),
                cancellationToken);
        }
        return true;
    }

    private async Task<(Competition Competition, Contest[] Contests)?> LoadCompetitionAsync(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(item => item.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return null;
        var stageIds = competition.Editions.SelectMany(item => item.Stages)
            .Select(item => item.Id).ToArray();
        var contests = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Officials)
            .Where(item => item.StageId.HasValue && stageIds.Contains(item.StageId.Value))
            .ToArrayAsync(cancellationToken);
        return (competition, contests);
    }

    private static CompetitionScheduleEvent ToScheduleEvent(
        Contest contest,
        CompetitionSchedulePolicy policy) => new(
        contest.Id,
        contest.Name ?? $"Contest {contest.SortOrder}",
        contest.Venue,
        contest.StartTime,
        contest.ScheduledDurationMinutes > 0
            ? contest.ScheduledDurationMinutes
            : policy.DefaultDurationMinutes,
        contest.Participants.Select(item => item.ParticipantId).ToArray(),
        contest.Officials.Select(item => item.OfficialId).ToArray());

    private async Task RemoveOverridesAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var overrides = await _database.ScheduleConflictOverrides
            .Where(item => item.CompetitionId == competitionId)
            .ToArrayAsync(cancellationToken);
        _database.ScheduleConflictOverrides.RemoveRange(overrides);
    }
}
