using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Notifications.Entities;

public sealed class PushDeviceRegistration : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public string? DeviceId { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    private PushDeviceRegistration() { }

    public PushDeviceRegistration(
        Guid id,
        Guid userId,
        string token,
        string platform,
        string? deviceId) : base(id)
    {
        Register(userId, token, platform, deviceId);
        CreatedAt = DateTime.UtcNow;
    }

    public void Register(Guid userId, string token, string platform, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A push token is required.", nameof(token));
        UserId = userId;
        Token = token.Trim();
        Platform = string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim().ToLowerInvariant();
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        IsEnabled = true;
        LastSeenAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
