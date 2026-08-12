using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public sealed class ConsentRecord : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public ConsentKind Kind { get; private set; }
    public bool Granted { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ConsentRecord() { }

    public ConsentRecord(
        Guid id,
        Guid userId,
        Guid? tenantId,
        ConsentKind kind,
        bool granted,
        string policyVersion,
        string source,
        string? ipAddress) : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        Kind = kind;
        Granted = granted;
        PolicyVersion = policyVersion.Trim();
        Source = source.Trim();
        IpAddress = ipAddress;
        OccurredAt = DateTime.UtcNow;
    }
}

public sealed class PrivacyControlSettings : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public ProfileVisibilityLevel ProfileVisibility { get; private set; }
    public bool ShowActivity { get; private set; }
    public bool AllowAnalytics { get; private set; }
    public bool AllowMarketing { get; private set; }

    private PrivacyControlSettings() { }

    public PrivacyControlSettings(Guid id, Guid userId, Guid? tenantId) : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        ProfileVisibility = ProfileVisibilityLevel.Private;
    }

    public void Update(
        ProfileVisibilityLevel profileVisibility,
        bool showActivity,
        bool allowAnalytics,
        bool allowMarketing)
    {
        ProfileVisibility = profileVisibility;
        ShowActivity = showActivity;
        AllowAnalytics = allowAnalytics;
        AllowMarketing = allowMarketing;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class PrivacyActionRequest : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public PrivacyRequestKind Kind { get; private set; }
    public PrivacyRequestState Status { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public string? Response { get; private set; }
    public string? ExportPayload { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private PrivacyActionRequest() { }

    public PrivacyActionRequest(
        Guid id,
        Guid userId,
        Guid? tenantId,
        PrivacyRequestKind kind,
        string details,
        string? exportPayload = null) : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        Kind = kind;
        Details = details.Trim();
        ExportPayload = exportPayload;
        RequestedAt = DateTime.UtcNow;
        Status = exportPayload is null ? PrivacyRequestState.Submitted : PrivacyRequestState.Completed;
        CompletedAt = exportPayload is null ? null : DateTime.UtcNow;
    }

    public void Review(Guid reviewerId, bool approved, string response)
    {
        if (Status is PrivacyRequestState.Completed or PrivacyRequestState.Rejected)
            throw new InvalidOperationException("The privacy request already has a final decision.");

        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        Response = response.Trim();
        Status = approved ? PrivacyRequestState.Approved : PrivacyRequestState.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string response)
    {
        if (Status != PrivacyRequestState.Approved)
            throw new InvalidOperationException("Only approved privacy requests can be completed.");
        Response = response.Trim();
        Status = PrivacyRequestState.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Redact()
    {
        Details = "Retained request metadata; subject-provided details removed.";
        Response = Response is null ? null : "Retained decision metadata; response removed.";
        ExportPayload = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class TenantRetentionPolicy : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public RetentionResource Resource { get; private set; }
    public int RetentionDays { get; private set; }
    public RetentionAction Action { get; private set; }
    public bool IsActive { get; private set; }
    public int RunIntervalHours { get; private set; }
    public DateTime NextRunAt { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public int LastAffectedCount { get; private set; }

    private TenantRetentionPolicy() { }

    public TenantRetentionPolicy(
        Guid id,
        Guid tenantId,
        string name,
        RetentionResource resource,
        int retentionDays,
        RetentionAction action,
        int runIntervalHours,
        bool isActive) : base(id)
    {
        TenantId = tenantId;
        Apply(name, resource, retentionDays, action, runIntervalHours, isActive);
        NextRunAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        RetentionResource resource,
        int retentionDays,
        RetentionAction action,
        int runIntervalHours,
        bool isActive)
    {
        Apply(name, resource, retentionDays, action, runIntervalHours, isActive);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkExecuted(int affectedCount)
    {
        LastRunAt = DateTime.UtcNow;
        LastAffectedCount = affectedCount;
        NextRunAt = DateTime.UtcNow.AddHours(RunIntervalHours);
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(
        string name,
        RetentionResource resource,
        int retentionDays,
        RetentionAction action,
        int runIntervalHours,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Policy name is required.");
        if (retentionDays is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (runIntervalHours is < 1 or > 8760) throw new ArgumentOutOfRangeException(nameof(runIntervalHours));
        if (resource != RetentionResource.PrivacyRequests && action == RetentionAction.Anonymize)
            throw new InvalidOperationException("Anonymization is supported only for privacy requests.");

        Name = name.Trim();
        Resource = resource;
        RetentionDays = retentionDays;
        Action = action;
        RunIntervalHours = runIntervalHours;
        IsActive = isActive;
    }
}

public enum ConsentKind { TermsOfService, PrivacyPolicy, Marketing, Analytics, DataProcessing }
public enum ProfileVisibilityLevel { Public, TenantOnly, Private }
public enum PrivacyRequestKind { DataAccess, DataCorrection, AccountDeletion }
public enum PrivacyRequestState { Submitted, Approved, Rejected, Completed }
public enum RetentionResource { Notifications, DeviceSessions, PrivacyRequests }
public enum RetentionAction { Delete, Anonymize }
