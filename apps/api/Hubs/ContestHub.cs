using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Tenancy.Entities;
using SportsPlatform.Modules.Notifications.Services;

namespace SportsPlatform.Api.Hubs;

/// <summary>
/// SignalR hub for real-time contest updates.
/// </summary>
public class ContestHub : Hub
{
    private readonly ILogger<ContestHub> _logger;
    private readonly IContestsService _contestsService;
    private readonly SportsPlatformDbContext _database;
    private readonly ICompetitionsService _competitionsService;
    private readonly OperationalMetrics _metrics;

    public ContestHub(
        ILogger<ContestHub> logger,
        IContestsService contestsService,
        ICompetitionsService competitionsService,
        SportsPlatformDbContext database,
        OperationalMetrics metrics)
    {
        _logger = logger;
        _contestsService = contestsService;
        _competitionsService = competitionsService;
        _database = database;
        _metrics = metrics;
    }

    /// <summary>
    /// Gets the tenant ID from the authenticated user context.
    /// </summary>
    private Guid? GetTenantId()
    {
        var tenantClaim = Context.User?.FindFirst("tenant_id");
        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            return tenantId;
        }
        return null;
    }

    /// <summary>
    /// Join a group for a specific contest.
    /// </summary>
    public async Task JoinContestGroup(Guid contestId)
    {
        var tenantId = await AuthorizedContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("ContestSnapshot", await BuildSnapshotAsync(contestId));
        _logger.LogInformation(
            "User {ConnectionId} joined contest group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows anonymous spectators to subscribe only to a contest that is public
    /// for the active tenant identified by its slug.
    /// </summary>
    public async Task JoinPublicContestGroup(string tenantSlug, Guid contestId)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            throw new HubException("The public contest is unavailable.");

        var isPublic = await _database.Contests.AsNoTracking()
            .Join(
                _database.Tenants.AsNoTracking(),
                contest => contest.TenantId,
                tenant => tenant.Id,
                (contest, tenant) => new { Contest = contest, Tenant = tenant })
            .AnyAsync(item =>
                item.Contest.Id == contestId
                && item.Tenant.Slug == tenantSlug
                && item.Tenant.Status == TenantStatus.Active
                && item.Contest.Status != ContestStatus.Draft
                && item.Contest.Status != ContestStatus.Cancelled);
        if (!isPublic)
            throw new HubException("The public contest is unavailable.");

        var groupName = ContestGroup(contestId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("ContestSnapshot", await BuildSnapshotAsync(contestId));
    }

    public Task LeavePublicContestGroup(Guid contestId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ContestGroup(contestId));

    /// <summary>
    /// Leave a group for a specific contest.
    /// </summary>
    public async Task LeaveContestGroup(Guid contestId)
    {
        var tenantId = await AuthorizedContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "User {ConnectionId} left contest group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Join a group for a specific competition.
    /// </summary>
    public async Task JoinCompetitionGroup(Guid competitionId)
    {
        var tenantId = await AuthorizedCompetitionTenantAsync(competitionId);
        var groupName = GetCompetitionGroupName(competitionId, tenantId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "User {ConnectionId} joined competition group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a group for a specific competition.
    /// </summary>
    public async Task LeaveCompetitionGroup(Guid competitionId)
    {
        var tenantId = await AuthorizedCompetitionTenantAsync(competitionId);
        var groupName = GetCompetitionGroupName(competitionId, tenantId);
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "User {ConnectionId} left competition group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Subscribe to updates for a specific segment within a contest.
    /// </summary>
    public async Task JoinSegmentGroup(Guid contestId, Guid segmentId)
    {
        var tenantId = await AuthorizedSegmentTenantAsync(contestId, segmentId);
        var groupName = GetSegmentGroupName(contestId, segmentId, tenantId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "User {ConnectionId} joined segment group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Unsubscribe from segment updates.
    /// </summary>
    public async Task LeaveSegmentGroup(Guid contestId, Guid segmentId)
    {
        var tenantId = await AuthorizedSegmentTenantAsync(contestId, segmentId);
        var groupName = GetSegmentGroupName(contestId, segmentId, tenantId);
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "User {ConnectionId} left segment group {GroupName}",
            Context.ConnectionId, groupName);
    }

    public async Task JoinTenantGroup(Guid tenantId)
    {
        if (Context.User is null || !Context.User.CanAccessTenant(tenantId))
            throw new HubException("The tenant group is unavailable for this user.");
        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
    }

    public Task LeaveTenantGroup(Guid tenantId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantGroup(tenantId));

    public async Task JoinParticipantGroup(Guid participantId)
    {
        var participant = await _database.Participants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == participantId);
        if (participant is null || Context.User is null
            || !Context.User.CanAccessTenant(participant.TenantId))
            throw new HubException("The participant group is unavailable for this tenant.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ParticipantGroup(participantId));
    }

    public Task LeaveParticipantGroup(Guid participantId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ParticipantGroup(participantId));

    public async Task JoinUserGroup(Guid userId)
    {
        if (Context.User?.UserId() != userId)
            throw new HubException("A user can subscribe only to their own group.");
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
    }

    public Task LeaveUserGroup(Guid userId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        _metrics.ConnectionOpened(Context.ConnectionId, tenantId);
        _logger.LogInformation(
            "User connected: {ConnectionId}, Tenant: {TenantId}",
            Context.ConnectionId, tenantId);
        
        if (tenantId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId.Value));
        var userId = Context.User?.UserId();
        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantId();
        _metrics.ConnectionClosed(Context.ConnectionId);
        _logger.LogInformation(
            "User disconnected: {ConnectionId}, Tenant: {TenantId}, Exception: {Exception}",
            Context.ConnectionId, tenantId, exception?.Message);
        
        await base.OnDisconnectedAsync(exception);
    }

    internal static string TenantGroup(Guid tenantId) => $"tenant:{tenantId}";
    internal static string ContestGroup(Guid contestId) => $"contest:{contestId}";
    internal static string ParticipantGroup(Guid participantId) => $"participant:{participantId}";
    internal static string UserGroup(Guid userId) => $"user:{userId}";

    private static string GetContestGroupName(Guid contestId, Guid? tenantId) =>
        ContestGroup(contestId);

    private static string GetCompetitionGroupName(Guid competitionId, Guid? tenantId)
    {
        return $"competition:{competitionId}";
    }

    private static string GetSegmentGroupName(Guid contestId, Guid segmentId, Guid? tenantId)
    {
        return $"segment:{segmentId}";
    }

    private async Task<Guid> AuthorizedContestTenantAsync(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null || Context.User is null ||
            !Context.User.CanAccessTenant(contest.TenantId))
            throw new HubException("The contest is unavailable for this tenant.");
        return contest.TenantId;
    }

    private async Task<Guid> AuthorizedCompetitionTenantAsync(Guid competitionId)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(competitionId);
        if (competition is null || Context.User is null ||
            !Context.User.CanAccessTenant(competition.TenantId))
            throw new HubException("The competition is unavailable for this tenant.");
        return competition.TenantId;
    }

    private async Task<Guid> AuthorizedSegmentTenantAsync(Guid contestId, Guid segmentId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        var segmentExists = contest?.Segments.Any(segment =>
            segment.Id == segmentId ||
            segment.NestedSegments.Any(nested => nested.Id == segmentId)) == true;
        if (!segmentExists || Context.User is null ||
            !Context.User.CanAccessTenant(contest!.TenantId))
            throw new HubException("The contest segment is unavailable for this tenant.");
        return contest.TenantId;
    }

    private async Task<ContestSnapshotDto> BuildSnapshotAsync(Guid contestId)
    {
        var contest = await _database.Contests
            .Include(item => item.Participants)
            .SingleAsync(item => item.Id == contestId);
        var scores = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderByDescending(item => item.ServerTimestamp)
            .Take(25)
            .OrderBy(item => item.ServerTimestamp)
            .Select(item => new LiveScoreUpdateDto(
                item.ContestId,
                item.Id,
                item.ParticipantId,
                item.MetricName,
                item.Value,
                item.DisplayValue,
                "Snapshot",
                item.ServerTimestamp))
            .ToListAsync();
        var session = await _database.LiveScoreSessions.AsNoTracking()
            .Where(item => item.ContestId == contestId
                && (item.Status == SessionStatus.Active
                    || item.Status == SessionStatus.Paused))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        var participantScores = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId
                && item.MetricName == "score")
            .GroupBy(item => item.ParticipantId)
            .Select(group => new ContestLiveParticipantDto(
                group.Key,
                group.Sum(item => item.Value).ToString(),
                null))
            .ToListAsync();
        return new ContestSnapshotDto(
            contest.Id,
            contest.Status.ToString(),
            scores.LastOrDefault()?.ScoredAt ?? contest.UpdatedAt ?? contest.CreatedAt,
            scores,
            contest.Participants.Select(item => new ParticipantStatusChangeDto(
                item.ParticipantId,
                item.Status.ToString(),
                item.Status.ToString(),
                null,
                contest.UpdatedAt ?? contest.CreatedAt)).ToList(),
            session?.ActiveSegmentId,
            participantScores);
    }
}

/// <summary>
/// Service for broadcasting contest updates via SignalR.
/// </summary>
public class ContestHubNotifier
{
    private readonly IHubContext<ContestHub> _hubContext;
    private readonly ILogger<ContestHubNotifier> _logger;
    private readonly IContestsService _contestsService;
    private readonly SportsPlatformDbContext _database;
    private readonly IPushDeliveryClient _pushDelivery;

    public ContestHubNotifier(
        IHubContext<ContestHub> hubContext,
        ILogger<ContestHubNotifier> logger,
        IContestsService contestsService,
        SportsPlatformDbContext database,
        IPushDeliveryClient pushDelivery)
    {
        _hubContext = hubContext;
        _logger = logger;
        _contestsService = contestsService;
        _database = database;
        _pushDelivery = pushDelivery;
    }

    /// <summary>
    /// Broadcasts a live score update to all clients watching a contest.
    /// </summary>
    public async Task BroadcastLiveScoreUpdate(Guid contestId, Guid? tenantId, LiveScoreUpdateDto update)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        var competitionId = await ResolveCompetitionIdAsync(contestId);
        var groups = new List<string>
        {
            groupName,
            ContestHub.TenantGroup(tenantId.Value),
            ContestHub.ParticipantGroup(update.ParticipantId)
        };
        if (competitionId.HasValue) groups.Add($"competition:{competitionId.Value}");
        await _hubContext.Clients.Groups(groups).SendAsync("LiveScoreUpdated", update);
        await BroadcastContestLivePatchAsync(contestId, groups);

        await SendContestPushAsync(
            contestId,
            $"score-{update.EventId:N}",
            PushNotificationTypes.ScoreChanged,
            "Score updated",
            string.IsNullOrWhiteSpace(update.DisplayValue)
                ? $"{update.MetricName}: {update.Value}"
                : $"{update.MetricName}: {update.DisplayValue}");
        
        _logger.LogDebug(
            "Broadcast live score update to group {GroupName}: {UpdateType}",
            groupName, update.UpdateType);
    }

    public async Task BroadcastContestStarted(Guid contestId, Guid? tenantId)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        await SendContestPushAsync(
            contestId,
            $"contest-started-{contestId:N}",
            PushNotificationTypes.ContestStarted,
            "Contest started",
            "The contest is now live.");
    }

    public async Task BroadcastQualification(
        Guid contestId,
        Guid participantId,
        Guid? tenantId)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var participantName = await _database.Participants.AsNoTracking()
            .Where(item => item.Id == participantId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync();
        await SendContestPushAsync(
            contestId,
            $"qualification-{contestId:N}-{participantId:N}",
            PushNotificationTypes.Qualification,
            "Qualification confirmed",
            $"{participantName ?? "The winner"} advanced to the next round.");
    }

    /// <summary>
    /// Broadcasts a contest status change.
    /// </summary>
    public async Task BroadcastContestStatusChange(Guid contestId, Guid? tenantId, string newStatus)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("ContestStatusChanged", new { ContestId = contestId, Status = newStatus });
        await BroadcastContestLivePatchAsync(contestId, [groupName]);
        
        _logger.LogDebug(
            "Broadcast contest status change to group {GroupName}: {Status}",
            groupName, newStatus);
    }

    /// <summary>
    /// Broadcasts a segment start event.
    /// </summary>
    public async Task BroadcastSegmentStart(Guid contestId, Guid segmentId, Guid? tenantId, SegmentStartDto segment)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetSegmentGroupName(contestId, segmentId, tenantId);
        
        await _hubContext.Clients
            .Groups(groupName, GetContestGroupName(contestId, tenantId))
            .SendAsync("SegmentStarted", segment);
        await BroadcastContestLivePatchAsync(contestId,
            [GetContestGroupName(contestId, tenantId)], segment.SegmentName);
        
        _logger.LogDebug(
            "Broadcast segment start to group {GroupName}",
            groupName);
    }

    /// <summary>
    /// Broadcasts a segment end event.
    /// </summary>
    public async Task BroadcastSegmentEnd(Guid contestId, Guid segmentId, Guid? tenantId, SegmentEndDto segment)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetSegmentGroupName(contestId, segmentId, tenantId);
        
        await _hubContext.Clients
            .Groups(groupName, GetContestGroupName(contestId, tenantId))
            .SendAsync("SegmentEnded", segment);
        await BroadcastContestLivePatchAsync(contestId,
            [GetContestGroupName(contestId, tenantId)], $"{segment.SegmentName} ended");

        await SendContestPushAsync(
            contestId,
            $"period-ended-{segmentId:N}",
            PushNotificationTypes.PeriodEnded,
            $"{segment.SegmentName} ended",
            "Open the contest to see the latest score.");
        
        _logger.LogDebug(
            "Broadcast segment end to group {GroupName}",
            groupName);
    }

    /// <summary>
    /// Broadcasts a contest result.
    /// </summary>
    public async Task BroadcastContestResult(Guid contestId, Guid? tenantId, ContestResultDto result)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("ContestResultFinalized", result);
        await BroadcastContestLivePatchAsync(contestId, [groupName], "Final result");

        var winner = result.Results.OrderBy(item => item.Rank).FirstOrDefault();
        await SendContestPushAsync(
            contestId,
            $"final-result-{contestId:N}",
            PushNotificationTypes.FinalResult,
            "Final result",
            winner is null
                ? "The contest result is final."
                : $"The contest is final. Winning score: {winner.TotalScore}.");
        
        _logger.LogInformation(
            "Broadcast contest result to group {GroupName}",
            groupName);
    }

    /// <summary>
    /// Broadcasts a participant status change (check-in, withdrawal, disqualification).
    /// </summary>
    public async Task BroadcastParticipantStatusChange(
        Guid contestId, 
        Guid participantId, 
        Guid? tenantId, 
        ParticipantStatusChangeDto change)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await _hubContext.Clients.Groups(new[]
            {
                groupName,
                ContestHub.TenantGroup(tenantId.Value),
                ContestHub.ParticipantGroup(participantId)
            })
            .SendAsync("ParticipantStatusChanged", change);
        
        _logger.LogDebug(
            "Broadcast participant status change to group {GroupName}: {ParticipantId} -> {Status}",
            groupName, participantId, change.NewStatus);
    }

    /// <summary>
    /// Sends a snapshot of current contest state to a specific connection.
    /// Used for reconnection scenarios.
    /// </summary>
    public async Task SendContestSnapshot(string connectionId, Guid contestId, Guid? tenantId, ContestSnapshotDto snapshot)
    {
        await ResolveContestTenantAsync(contestId);
        
        await _hubContext.Clients
            .Client(connectionId)
            .SendAsync("ContestSnapshot", snapshot);
        
        _logger.LogDebug(
            "Sent contest snapshot to connection {ConnectionId}",
            connectionId);
    }

    /// <summary>
    /// Broadcasts a heartbeat/ping to keep connections alive.
    /// </summary>
    public async Task BroadcastHeartbeat(Guid contestId, Guid? tenantId)
    {
        tenantId = await ResolveContestTenantAsync(contestId);
        var groupName = GetContestGroupName(contestId, tenantId);
        
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("Heartbeat", new { Timestamp = DateTime.UtcNow });
    }

    private static string GetContestGroupName(Guid contestId, Guid? tenantId) =>
        ContestHub.ContestGroup(contestId);

    private static string GetSegmentGroupName(Guid contestId, Guid segmentId, Guid? tenantId)
    {
        return $"segment:{segmentId}";
    }

    private async Task<Guid> ResolveContestTenantAsync(Guid contestId)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId)
            ?? throw new InvalidOperationException(
                $"Contest {contestId} was not found while broadcasting an update.");
        return contest.TenantId;
    }

    private async Task<Guid?> ResolveCompetitionIdAsync(Guid contestId)
    {
        var stageId = await _database.Contests.AsNoTracking()
            .Where(item => item.Id == contestId)
            .Select(item => item.StageId)
            .SingleOrDefaultAsync();
        if (!stageId.HasValue) return null;
        return await _database.Competitions.AsNoTracking()
            .Where(item => item.Editions
                .SelectMany(edition => edition.Stages)
                .Any(stage => stage.Id == stageId.Value))
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync();
    }

    private async Task BroadcastContestLivePatchAsync(
        Guid contestId,
        IReadOnlyList<string> groups,
        string? summary = null)
    {
        var contest = await _database.Contests.AsNoTracking()
            .SingleAsync(item => item.Id == contestId);
        var scores = await _database.LiveScores.AsNoTracking()
            .Where(item => item.ContestId == contestId
                && item.MetricName == "score")
            .GroupBy(item => item.ParticipantId)
            .Select(group => new ContestLiveParticipantDto(
                group.Key,
                group.Sum(item => item.Value).ToString(),
                null))
            .ToListAsync();
        var updatedAt = contest.UpdatedAt ?? DateTime.UtcNow;
        await _hubContext.Clients.Groups(groups).SendAsync(
            "ContestLiveUpdated",
            new ContestLiveUpdateDto(
                contestId,
                new DateTimeOffset(updatedAt).ToUnixTimeMilliseconds(),
                contest.Status.ToString(),
                scores,
                summary));
    }

    private async Task SendContestPushAsync(
        Guid contestId,
        string notificationId,
        string type,
        string title,
        string body)
    {
        if (!_pushDelivery.IsConfigured) return;

        var context = await _database.Contests.AsNoTracking()
            .Where(item => item.Id == contestId)
            .Join(
                _database.Tenants.AsNoTracking(),
                contest => contest.TenantId,
                tenant => tenant.Id,
                (contest, tenant) => new
                {
                    TenantSlug = tenant.Slug,
                    ContestName = contest.Name
                })
            .SingleAsync();
        var competitionId = await ResolveCompetitionIdAsync(contestId);
        var data = new Dictionary<string, string>
        {
            ["contestId"] = contestId.ToString(),
            ["tenantSlug"] = context.TenantSlug
        };
        if (competitionId.HasValue)
            data["competitionId"] = competitionId.Value.ToString();

        await _pushDelivery.SendToTopicsAsync(
            PushTopics.ContestAlert(context.TenantSlug, contestId, competitionId, type),
            new PushDelivery(
                notificationId,
                type,
                title,
                string.IsNullOrWhiteSpace(context.ContestName)
                    ? body
                    : $"{context.ContestName}: {body}",
                $"contest:{contestId}",
                data));
    }
}

/// <summary>
/// DTO for live score updates.
/// </summary>
public record LiveScoreUpdateDto(
    Guid ContestId,
    Guid EventId,
    Guid ParticipantId,
    string MetricName,
    decimal Value,
    string? DisplayValue,
    string UpdateType,
    DateTime ScoredAt);

public record ContestLiveParticipantDto(
    Guid ParticipantId,
    string? Score,
    string? ScoreDisplay);

public record ContestLiveUpdateDto(
    Guid ContestId,
    long Version,
    string Status,
    List<ContestLiveParticipantDto> Participants,
    string? Summary);

/// <summary>
/// DTO for segment start.
/// </summary>
public record SegmentStartDto(
    Guid SegmentId,
    string SegmentName,
    int SegmentNumber,
    DateTime StartedAt);

/// <summary>
/// DTO for segment end.
/// </summary>
public record SegmentEndDto(
    Guid SegmentId,
    string SegmentName,
    DateTime EndedAt,
    Dictionary<Guid, decimal> SegmentScores);

/// <summary>
/// DTO for contest result.
/// </summary>
public record ContestResultDto(
    Guid ContestId,
    string Status,
    DateTime FinalizedAt,
    List<ParticipantResultDto> Results);

/// <summary>
/// DTO for participant result.
/// </summary>
public record ParticipantResultDto(
    Guid ParticipantId,
    int Rank,
    decimal TotalScore,
    string? Medal,
    Dictionary<string, decimal> MetricScores);

/// <summary>
/// DTO for participant status change.
/// </summary>
public record ParticipantStatusChangeDto(
    Guid ParticipantId,
    string OldStatus,
    string NewStatus,
    string? Reason,
    DateTime ChangedAt);

/// <summary>
/// DTO for contest snapshot (used for reconnection).
/// </summary>
public record ContestSnapshotDto(
    Guid ContestId,
    string Status,
    DateTime LastUpdated,
    List<LiveScoreUpdateDto> RecentScores,
    List<ParticipantStatusChangeDto> ParticipantStatuses,
    Guid? CurrentSegmentId,
    List<ContestLiveParticipantDto> ParticipantScores);
