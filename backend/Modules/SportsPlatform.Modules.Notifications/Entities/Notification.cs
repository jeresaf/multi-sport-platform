using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Notifications.Entities;

public class Notification : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? Metadata { get; private set; }
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;
    public DateTime? SentAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Notification() { }

    public Notification(Guid id, Guid userId, NotificationType type, NotificationChannel channel, string title, string content) : base(id)
    {
        UserId = userId;
        Type = type;
        Channel = channel;
        Title = title;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetMetadata(string? metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        Status = NotificationStatus.Read;
        ReadAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string? errorMessage)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Retry()
    {
        if (Status != NotificationStatus.Failed)
            throw new InvalidOperationException("Only failed notifications can be retried.");
        
        Status = NotificationStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum NotificationType
{
    System = 0,
    Contest = 1,
    Registration = 2,
    Result = 3,
    Reminder = 4,
    Alert = 5,
    Marketing = 6,
    FixtureReminder = 7,
    ContestStarted = 8,
    ScoreChanged = 9,
    PeriodEnded = 10,
    FinalResult = 11,
    Postponement = 12,
    Qualification = 13,
    Announcement = 14
}

public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Sms = 2,
    Push = 3
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Read = 2,
    Failed = 3
}
