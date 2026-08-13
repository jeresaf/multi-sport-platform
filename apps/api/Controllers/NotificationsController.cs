using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Notifications.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IPushDeliveryClient _pushDelivery;
    private readonly IAuditService _audit;

    public NotificationsController(
        SportsPlatformDbContext database,
        IPushDeliveryClient pushDelivery,
        IAuditService audit)
    {
        _database = database;
        _pushDelivery = pushDelivery;
        _audit = audit;
    }

    [HttpGet("campaigns")]
    [RequirePermission("notifications.read")]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var campaigns = await _database.NotificationCampaigns.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                item.Id,
                type = item.Type.ToString(),
                status = item.Status.ToString(),
                item.Title,
                item.Body,
                item.Reason,
                item.CompetitionId,
                item.ContestId,
                item.RecipientCount,
                item.CreatedBy,
                item.CreatedAt,
                item.DispatchedAt
            })
            .ToArrayAsync(cancellationToken);
        return Ok(campaigns);
    }

    [HttpPost("campaigns")]
    [RequirePermission("notifications.manage")]
    public async Task<IActionResult> CreateCampaign(
        [FromBody] CreateNotificationCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Body)
            || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Title, message, audit reason, and idempotency key are required." });
        if (!Enum.TryParse<NotificationCampaignType>(request.Type, true, out var type))
            return BadRequest(new { error = "Type must be FixtureReminder or Announcement." });

        var tenant = await _database.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.TenantId, cancellationToken);
        if (tenant is null) return NotFound(new { error = "Tenant not found." });

        Guid? competitionId = request.CompetitionId;
        if (request.ContestId.HasValue)
        {
            var stageId = await _database.Contests.AsNoTracking()
                .Where(item => item.Id == request.ContestId.Value
                    && item.TenantId == request.TenantId)
                .Select(item => item.StageId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!stageId.HasValue) return BadRequest(new { error = "The contest does not belong to this tenant." });
            var owningCompetitionId = await _database.Competitions.AsNoTracking()
                .Where(item => item.TenantId == request.TenantId
                    && item.Editions.SelectMany(edition => edition.Stages)
                        .Any(stage => stage.Id == stageId.Value))
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (!owningCompetitionId.HasValue)
                return BadRequest(new { error = "The contest is not attached to a competition." });
            if (competitionId.HasValue && competitionId != owningCompetitionId)
                return BadRequest(new { error = "The contest does not belong to the selected competition." });
            competitionId = owningCompetitionId;
        }
        else if (competitionId.HasValue && !await _database.Competitions.AsNoTracking().AnyAsync(
                     item => item.Id == competitionId.Value
                         && item.TenantId == request.TenantId, cancellationToken))
        {
            return BadRequest(new { error = "The competition does not belong to this tenant." });
        }

        var existing = await _database.NotificationCampaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == request.TenantId
                && item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (!Matches(existing, type, request.Title, request.Body, request.Reason,
                    competitionId, request.ContestId))
                return Conflict(new { error = "The idempotency key was already used for a different campaign." });
            return Ok(ToCampaignResponse(existing));
        }

        try
        {
            var campaign = new NotificationCampaign(
                Guid.NewGuid(), request.TenantId, type, request.Title, request.Body,
                request.IdempotencyKey, request.Reason, actorId.Value,
                competitionId, request.ContestId);
            var recipients = await _database.Users.AsNoTracking()
                .Where(item => item.TenantId == request.TenantId
                    && item.IsActive && !item.IsSuspended)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var notificationType = type == NotificationCampaignType.FixtureReminder
                ? NotificationType.FixtureReminder
                : NotificationType.Announcement;
            var metadata = JsonSerializer.Serialize(new
            {
                campaignId = campaign.Id,
                tenantId = request.TenantId,
                tenantSlug = tenant.Slug,
                competitionId,
                contestId = request.ContestId,
                type = CanonicalType(notificationType)
            });
            foreach (var recipientId in recipients)
            {
                var notification = new Notification(
                    Guid.NewGuid(), recipientId, notificationType,
                    NotificationChannel.InApp, campaign.Title, campaign.Body);
                notification.SetMetadata(metadata);
                _database.Notifications.Add(notification);
            }
            campaign.MarkDispatched(recipients.Length);
            _database.NotificationCampaigns.Add(campaign);
            await _database.SaveChangesAsync(cancellationToken);

            var payload = request.ContestId.HasValue
                ? $"contest:{request.ContestId.Value}"
                : competitionId.HasValue ? $"competition:{competitionId.Value}" : string.Empty;
            var topics = request.ContestId.HasValue
                ? PushTopics.Contest(tenant.Slug, request.ContestId.Value, competitionId)
                : competitionId.HasValue
                    ? new[] { PushTopics.Tenant(tenant.Slug), PushTopics.FavouriteCompetition(competitionId.Value) }
                    : new[] { PushTopics.Tenant(tenant.Slug) };
            await _pushDelivery.SendToTopicsAsync(
                topics,
                new PushDelivery(
                    campaign.Id.ToString("N"), CanonicalType(notificationType),
                    campaign.Title, campaign.Body, payload,
                    new Dictionary<string, string>
                    {
                        ["tenantId"] = request.TenantId.ToString(),
                        ["tenantSlug"] = tenant.Slug,
                        ["campaignId"] = campaign.Id.ToString(),
                        ["competitionId"] = competitionId?.ToString() ?? string.Empty,
                        ["contestId"] = request.ContestId?.ToString() ?? string.Empty
                    }),
                cancellationToken);
            await _audit.RecordAsync(
                "NotificationCampaignDispatched", "NotificationCampaign",
                campaign.Id.ToString(), request.TenantId, actorId,
                null, JsonSerializer.Serialize(new
                {
                    type = type.ToString(), competitionId, request.ContestId,
                    campaign.RecipientCount, campaign.IdempotencyKey
                }), campaign.Reason, cancellationToken);
            return CreatedAtAction(
                nameof(GetCampaigns), new { tenantId = request.TenantId },
                ToCampaignResponse(campaign));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DbUpdateException)
        {
            _database.ChangeTracker.Clear();
            var raced = await _database.NotificationCampaigns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == request.TenantId
                    && item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
            if (raced is not null)
                return Matches(raced, type, request.Title, request.Body, request.Reason,
                        competitionId, request.ContestId)
                    ? Ok(ToCampaignResponse(raced))
                    : Conflict(new { error = "The idempotency key was already used for a different campaign." });
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { error = "Page must be positive and pageSize must be between 1 and 100." });

        var query = _database.Notifications.AsNoTracking()
            .Where(item => item.UserId == userId.Value);
        if (unreadOnly)
            query = query.Where(item => item.Status != NotificationStatus.Read);

        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return Ok(new
        {
            data = notifications.Select(ToResponse),
            page,
            pageSize,
            total
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var count = await _database.Notifications.AsNoTracking().CountAsync(
            item => item.UserId == userId.Value
                && item.Status != NotificationStatus.Read,
            cancellationToken);
        return Ok(new { count });
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await FindOwnedAsync(notificationId, cancellationToken);
        if (notification is null) return NotFound();
        if (notification.Status != NotificationStatus.Read)
        {
            notification.MarkAsRead();
            await _database.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var notifications = await _database.Notifications
            .Where(item => item.UserId == userId.Value
                && item.Status != NotificationStatus.Read)
            .ToArrayAsync(cancellationToken);
        foreach (var notification in notifications) notification.MarkAsRead();
        await _database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await FindOwnedAsync(notificationId, cancellationToken);
        if (notification is null) return NotFound();
        _database.Notifications.Remove(notification);
        await _database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/current")]
    public async Task<IActionResult> RegisterCurrentDevice(
        [FromBody] RegisterPushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 2048)
            return BadRequest(new { error = "A valid push token is required." });
        var platform = request.Platform.Trim().ToLowerInvariant();
        if (platform is not ("android" or "ios" or "web"))
            return BadRequest(new { error = "Platform must be android, ios, or web." });

        string? tenantTopic = null;
        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var tenantSlug = request.TenantSlug.Trim();
            var tenantExists = await _database.Tenants.AsNoTracking()
                .AnyAsync(item => item.Slug == tenantSlug, cancellationToken);
            if (!tenantExists)
                return BadRequest(new { error = "The selected tenant was not found." });
            tenantTopic = PushTopics.Tenant(tenantSlug);
        }

        var token = request.Token.Trim();
        var registration = await _database.PushDeviceRegistrations
            .SingleOrDefaultAsync(item => item.Token == token, cancellationToken);
        if (registration is null && !string.IsNullOrWhiteSpace(request.DeviceId))
        {
            registration = await _database.PushDeviceRegistrations
                .Where(item => item.UserId == userId.Value
                    && item.DeviceId == request.DeviceId)
                .OrderByDescending(item => item.LastSeenAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (registration is null)
        {
            registration = new PushDeviceRegistration(
                Guid.NewGuid(), userId.Value, token, platform, request.DeviceId);
            _database.PushDeviceRegistrations.Add(registration);
        }
        else
        {
            registration.Register(
                userId.Value, token, platform, request.DeviceId);
        }
        await _database.SaveChangesAsync(cancellationToken);

        if (tenantTopic is not null)
        {
            if (!await _pushDelivery.SetTopicSubscriptionAsync(
                    token, tenantTopic, true, cancellationToken))
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "The device was registered, but Firebase could not synchronize its tenant topic." });
        }

        return Ok(new
        {
            registration.Id,
            registration.Platform,
            registration.IsEnabled,
            registration.LastSeenAt,
            tenantTopic
        });
    }

    [HttpDelete("devices/current")]
    public async Task<IActionResult> DisableCurrentDevice(
        [FromBody] DisablePushDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var token = request.Token?.Trim();
        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(deviceId))
            return BadRequest(new { error = "A token or deviceId is required." });
        var registrations = await _database.PushDeviceRegistrations
            .Where(item => item.UserId == userId.Value
                && item.IsEnabled
                && ((!string.IsNullOrEmpty(token) && item.Token == token)
                    || (!string.IsNullOrEmpty(deviceId) && item.DeviceId == deviceId)))
            .ToArrayAsync(cancellationToken);
        foreach (var registration in registrations) registration.Disable();
        await _database.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            var topic = PushTopics.Tenant(request.TenantSlug.Trim());
            foreach (var registration in registrations)
            {
                await _pushDelivery.SetTopicSubscriptionAsync(
                    registration.Token, topic, false, cancellationToken);
            }
        }
        return NoContent();
    }

    [HttpPut("devices/current/topics")]
    public async Task<IActionResult> SetCurrentDeviceTopic(
        [FromBody] SetPushDeviceTopicRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { error = "A push token and topic are required." });

        var token = request.Token.Trim();
        var topic = PushTopics.Normalize(request.Topic.Trim());
        if (topic is not { Length: > 0 }
            || !(topic.StartsWith("tenant-", StringComparison.Ordinal)
                || topic.StartsWith("favourite-contest-", StringComparison.Ordinal)
                || topic.StartsWith("favourite-competition-", StringComparison.Ordinal)))
            return BadRequest(new { error = "The push topic is not supported." });

        var registration = await _database.PushDeviceRegistrations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId.Value
                && item.Token == token
                && item.IsEnabled, cancellationToken);
        if (registration is null) return NotFound(new { error = "The push device is not registered." });

        var synchronized = await _pushDelivery.SetTopicSubscriptionAsync(
            token, topic, request.Enabled, cancellationToken);
        if (!synchronized)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Firebase could not synchronize the device topic." });

        return Ok(new { topic, enabled = request.Enabled });
    }

    private async Task<Notification?> FindOwnedAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return null;
        return await _database.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == userId.Value,
            cancellationToken);
    }

    private static object ToResponse(Notification notification)
    {
        var data = ParseMetadata(notification.Metadata);
        data["type"] = CanonicalType(notification.Type);
        data["channel"] = notification.Channel.ToString().ToLowerInvariant();
        data["isRead"] = notification.Status == NotificationStatus.Read;
        var payload = Payload(data);
        return new
        {
            id = notification.Id,
            title = notification.Title,
            body = notification.Content,
            payload,
            data,
            timestamp = notification.CreatedAt
        };
    }

    private static string CanonicalType(NotificationType type) => type switch
    {
        NotificationType.FixtureReminder => PushNotificationTypes.FixtureReminder,
        NotificationType.ContestStarted => PushNotificationTypes.ContestStarted,
        NotificationType.ScoreChanged => PushNotificationTypes.ScoreChanged,
        NotificationType.PeriodEnded => PushNotificationTypes.PeriodEnded,
        NotificationType.FinalResult => PushNotificationTypes.FinalResult,
        NotificationType.Postponement => PushNotificationTypes.Postponement,
        NotificationType.Qualification => PushNotificationTypes.Qualification,
        NotificationType.Announcement => PushNotificationTypes.Announcement,
        _ => type.ToString().ToLowerInvariant()
    };

    private static object ToCampaignResponse(NotificationCampaign campaign) => new
    {
        campaign.Id,
        type = campaign.Type.ToString(),
        status = campaign.Status.ToString(),
        campaign.Title,
        campaign.Body,
        campaign.Reason,
        campaign.CompetitionId,
        campaign.ContestId,
        campaign.RecipientCount,
        campaign.CreatedBy,
        campaign.CreatedAt,
        campaign.DispatchedAt
    };

    private static bool Matches(
        NotificationCampaign campaign,
        NotificationCampaignType type,
        string title,
        string body,
        string reason,
        Guid? competitionId,
        Guid? contestId) =>
        campaign.Type == type
        && campaign.Title == title.Trim()
        && campaign.Body == body.Trim()
        && campaign.Reason == reason.Trim()
        && campaign.CompetitionId == competitionId
        && campaign.ContestId == contestId;

    private static Dictionary<string, object?> ParseMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(metadata)
                ?? new();
        }
        catch (JsonException)
        {
            return new() { ["metadata"] = metadata };
        }
    }

    private static string? Payload(IReadOnlyDictionary<string, object?> data)
    {
        if (TryValue(data, "contestId", out var contestId))
            return $"contest:{contestId}";
        if (TryValue(data, "competitionId", out var competitionId))
            return $"competition:{competitionId}";
        return null;
    }

    private static bool TryValue(
        IReadOnlyDictionary<string, object?> data,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!data.TryGetValue(key, out var raw) || raw is null) return false;
        value = raw is JsonElement element ? element.ToString() : raw.ToString() ?? string.Empty;
        return value.Length > 0;
    }
}

public sealed record RegisterPushDeviceRequest(
    string Token,
    string Platform,
    string? DeviceId,
    string? TenantSlug = null);

public sealed record DisablePushDeviceRequest(
    string? Token,
    string? DeviceId,
    string? TenantSlug = null);

public sealed record SetPushDeviceTopicRequest(
    string Token,
    string Topic,
    bool Enabled);

public sealed record CreateNotificationCampaignRequest(
    Guid TenantId,
    string Type,
    string Title,
    string Body,
    string IdempotencyKey,
    Guid? CompetitionId = null,
    Guid? ContestId = null,
    string? Reason = null);
