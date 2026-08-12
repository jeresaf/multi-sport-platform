using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Audit.Entities;

public class AuditEntry : Entity<Guid>
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? OriginalValue { get; private set; }
    public string? NewValue { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditEntry() { }

    public AuditEntry(
        Guid id,
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        string? originalValue,
        string? newValue,
        string? reason) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OriginalValue = originalValue;
        NewValue = newValue;
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Not supplied" : reason.Trim();
        Reason = normalizedReason.Length <= 1000 ? normalizedReason : normalizedReason[..1000];
        Timestamp = DateTime.UtcNow;
    }

    public void SetRequestContext(string? ipAddress, string? userAgent)
    {
        IpAddress = string.IsNullOrWhiteSpace(ipAddress)
            ? null
            : ipAddress.Trim()[..Math.Min(ipAddress.Trim().Length, 50)];
        UserAgent = string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Trim()[..Math.Min(userAgent.Trim().Length, 500)];
    }
}
