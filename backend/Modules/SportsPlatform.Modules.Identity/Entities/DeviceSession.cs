using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public class DeviceSession : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string DeviceName { get; private set; } = string.Empty;
    public string? DeviceType { get; private set; }
    public string? IpAddress { get; private set; }
    public string RefreshToken { get; private set; } = string.Empty;
    public DateTime RefreshTokenExpiry { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime? LastActivityAt { get; private set; }

    private DeviceSession() { }

    public DeviceSession(Guid id, Guid userId, string deviceId, string deviceName, string? deviceType, string? ipAddress, string refreshToken, DateTime refreshTokenExpiry) : base(id)
    {
        UserId = userId;
        DeviceId = deviceId;
        DeviceName = deviceName;
        DeviceType = deviceType;
        IpAddress = ipAddress;
        RefreshToken = refreshToken;
        RefreshTokenExpiry = refreshTokenExpiry;
        IsRevoked = false;
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordActivity()
    {
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateRefreshToken(string refreshTokenHash, DateTime expiry)
    {
        if (IsRevoked)
            throw new InvalidOperationException("A revoked device session cannot be refreshed.");
        RefreshToken = refreshTokenHash;
        RefreshTokenExpiry = expiry.ToUniversalTime();
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsExpired()
    {
        return RefreshTokenExpiry <= DateTime.UtcNow || IsRevoked;
    }
}
