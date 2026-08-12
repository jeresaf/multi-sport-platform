using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Notifications.Entities;

public sealed class NotificationCampaign : TenantAggregateRoot<Guid>
{
    public NotificationCampaignType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid? CompetitionId { get; private set; }
    public Guid? ContestId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public NotificationCampaignStatus Status { get; private set; }
    public int RecipientCount { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public new DateTime CreatedAt { get; private set; }

    private NotificationCampaign() { }

    public NotificationCampaign(
        Guid id,
        Guid tenantId,
        NotificationCampaignType type,
        string title,
        string body,
        string idempotencyKey,
        string reason,
        Guid createdBy,
        Guid? competitionId = null,
        Guid? contestId = null) : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160)
            throw new ArgumentException("A title between 1 and 160 characters is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 1000)
            throw new ArgumentException("A message between 1 and 1000 characters is required.", nameof(body));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
            throw new ArgumentException("An idempotency key between 1 and 100 characters is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("An audit reason between 1 and 500 characters is required.", nameof(reason));
        if (type == NotificationCampaignType.FixtureReminder && !contestId.HasValue)
            throw new ArgumentException("A fixture reminder requires a contest.", nameof(contestId));

        Type = type;
        Title = title.Trim();
        Body = body.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        Reason = reason.Trim();
        CreatedBy = createdBy;
        CompetitionId = competitionId;
        ContestId = contestId;
        Status = NotificationCampaignStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkDispatched(int recipientCount)
    {
        if (Status != NotificationCampaignStatus.Pending)
            throw new InvalidOperationException("Only pending campaigns can be dispatched.");
        if (recipientCount < 0) throw new ArgumentOutOfRangeException(nameof(recipientCount));
        RecipientCount = recipientCount;
        Status = NotificationCampaignStatus.Dispatched;
        DispatchedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum NotificationCampaignType
{
    FixtureReminder = 0,
    Announcement = 1
}

public enum NotificationCampaignStatus
{
    Pending = 0,
    Dispatched = 1
}
