using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public bool EmailVerified { get; private set; }
    public bool PhoneVerified { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsSuspended { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? PasswordChangedAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiry { get; private set; }
    public Guid? TenantId { get; private set; }
    public bool MfaEnabled { get; private set; }
    public string? MfaSecretProtected { get; private set; }
    public DateTime? MfaEnrolledAt { get; private set; }

    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private readonly List<DeviceSession> _sessions = new();
    public IReadOnlyCollection<DeviceSession> Sessions => _sessions.AsReadOnly();

    private User() { }

    public User(Guid id, string email, string firstName, string lastName, string password, Guid? tenantId = null) : base(id)
    {
        Email = email.Trim().ToLowerInvariant();
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        TenantId = tenantId;
        EmailVerified = false;
        IsActive = true;
        IsSuspended = false;
        FailedLoginAttempts = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public bool VerifyPassword(string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }

    public void ChangePassword(string currentPassword, string newPassword)
    {
        if (!VerifyPassword(currentPassword))
            throw new InvalidOperationException("Current password is incorrect.");

        PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        PasswordChangedAt = DateTime.UtcNow;
        RefreshToken = null;
        RefreshTokenExpiry = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetPassword(string newPassword)
    {
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        PasswordChangedAt = DateTime.UtcNow;
        RefreshToken = null;
        RefreshTokenExpiry = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void VerifyEmail()
    {
        EmailVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLockedOut()
    {
        return LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A suspension reason is required.", nameof(reason));
        IsSuspended = true;
        SuspendedAt = DateTime.UtcNow;
        SuspensionReason = reason.Trim();
        RevokeRefreshToken();
        RevokeAllSessions();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unsuspend()
    {
        IsSuspended = false;
        SuspendedAt = null;
        SuspensionReason = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiry = expiry;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiry = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void BeginMfaEnrollment(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
            throw new ArgumentException("An MFA secret is required.", nameof(protectedSecret));
        MfaSecretProtected = protectedSecret;
        MfaEnabled = false;
        MfaEnrolledAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnableMfa()
    {
        if (string.IsNullOrWhiteSpace(MfaSecretProtected))
            throw new InvalidOperationException("MFA enrollment has not started.");
        MfaEnabled = true;
        MfaEnrolledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecretProtected = null;
        MfaEnrolledAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CorrectProfile(string firstName, string lastName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AnonymizeForPrivacy()
    {
        Email = $"deleted+{Id:N}@privacy.invalid";
        FirstName = "Deleted";
        LastName = "User";
        PhoneNumber = null;
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        RefreshToken = null;
        RefreshTokenExpiry = null;
        DisableMfa();
        IsActive = false;
        IsSuspended = true;
        SuspendedAt = DateTime.UtcNow;
        SuspensionReason = "Account deleted through the privacy workflow.";
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRole(Guid roleId)
    {
        if (!_roles.Any(r => r.RoleId == roleId))
        {
            _roles.Add(new UserRole(Guid.NewGuid(), Id, roleId));
        }
    }

    public void RemoveRole(Guid roleId)
    {
        var role = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role != null)
        {
            _roles.Remove(role);
        }
    }

    public DeviceSession AddSession(string deviceId, string deviceName, string? deviceType, string? ipAddress, string refreshToken, DateTime expiry)
    {
        var session = new DeviceSession(Guid.NewGuid(), Id, deviceId, deviceName, deviceType, ipAddress, refreshToken, expiry);
        _sessions.Add(session);
        return session;
    }

    public DeviceSession ReplaceSession(
        string deviceId,
        string deviceName,
        string? deviceType,
        string? ipAddress,
        string refreshTokenHash,
        DateTime expiry)
    {
        foreach (var existing in _sessions.Where(session =>
                     !session.IsRevoked && session.DeviceId == deviceId))
            existing.Revoke();
        return AddSession(
            deviceId,
            deviceName,
            deviceType,
            ipAddress,
            refreshTokenHash,
            expiry);
    }

    public void RevokeSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null)
        {
            session.Revoke();
        }
    }

    public void RevokeAllSessions()
    {
        foreach (var session in _sessions)
        {
            session.Revoke();
        }
    }
}
