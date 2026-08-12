using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Identity.DTOs;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace SportsPlatform.Modules.Identity.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IMfaService _mfaService;
    private readonly IIdentityMessageSender _messageSender;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ITokenService tokenService,
        IAuditService auditService,
        IMfaService mfaService,
        IIdentityMessageSender messageSender,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _tokenService = tokenService;
        _auditService = auditService;
        _mfaService = mfaService;
        _messageSender = messageSender;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Validate email uniqueness
        var emailExists = await _userRepository.EmailExistsAsync(request.Email, cancellationToken);
        if (emailExists)
            throw new InvalidOperationException("Email is already registered.");

        // Create user
        var user = new User(Guid.NewGuid(), request.Email, request.FirstName, request.LastName, request.Password, request.TenantId);

        // Assign default Viewer role
        var viewerRole = await _roleRepository.GetByNameAsync(SystemRoles.Viewer, request.TenantId, cancellationToken);
        if (viewerRole != null)
        {
            user.AddRole(viewerRole.Id);
        }

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: User registration
        await _auditService.RecordAsync(
            "UserRegistered",
            "User",
            user.Id.ToString(),
            request.TenantId,
            user.Id,
            null,
            $"Email: {request.Email}, Name: {request.FirstName} {request.LastName}",
            cancellationToken: cancellationToken);

        // Generate tokens
        var roles = await GetUserRolesAsync(user, cancellationToken);
        var permissions = await GetUserPermissionsAsync(user, cancellationToken);
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var registrationSession = user.AddSession(
            string.IsNullOrWhiteSpace(request.DeviceId)
                ? $"registration-{Guid.NewGuid():N}"
                : request.DeviceId.Trim(),
            request.DeviceName ?? "Registration",
            request.DeviceType,
            null,
            HashToken(refreshToken),
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));
        await _userRepository.AddSessionAsync(registrationSession, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            refreshToken,
            expiresAt,
            MapToUserInfo(user, roles, permissions)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        // Check if user is suspended
        if (user.IsSuspended)
            throw new UnauthorizedAccessException("Account is suspended. Contact support.");

        // Check if user is locked out
        if (user.IsLockedOut())
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");

        // Verify password
        if (!user.VerifyPassword(request.Password))
        {
            user.RecordFailedLogin();
            await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

            // Audit: Failed login attempt
            await _auditService.RecordAsync(
                "UserLoginFailed",
                "User",
                user.Id.ToString(),
                user.TenantId,
                user.Id,
                null,
                $"Failed login attempt #{user.FailedLoginAttempts}",
                cancellationToken: cancellationToken);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.MfaEnabled && !_mfaService.VerifyCode(user, request.MfaCode))
            throw new UnauthorizedAccessException("A valid MFA code is required.");

        // Record successful login
        user.RecordLogin();

        // Generate tokens
        var roles = await GetUserRolesAsync(user, cancellationToken);
        var permissions = await GetUserPermissionsAsync(user, cancellationToken);
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles, permissions);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var deviceId = string.IsNullOrWhiteSpace(request.DeviceId)
            ? $"login-{Guid.NewGuid():N}"
            : request.DeviceId.Trim();
        var session = user.ReplaceSession(
            deviceId,
            request.DeviceName ?? "Unknown",
            request.DeviceType,
            request.IpAddress,
            HashToken(newRefreshToken),
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));
        await _userRepository.AddSessionAsync(session, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit after the complete login state has been persisted atomically.
        await _auditService.RecordAsync(
            "UserLogin",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            null,
            $"Device: {request.DeviceName ?? "Unknown"}",
            cancellationToken: cancellationToken);

        return new AuthResponse(
            accessToken,
            newRefreshToken,
            expiresAt,
            MapToUserInfo(user, roles, permissions)
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _userRepository.GetSessionByRefreshTokenHashAsync(
            HashToken(request.RefreshToken), cancellationToken);
        if (session == null || session.IsExpired())
            throw new UnauthorizedAccessException("Invalid refresh token.");
        if (!string.IsNullOrWhiteSpace(request.DeviceId)
            && !string.Equals(session.DeviceId, request.DeviceId.Trim(), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Refresh token does not belong to this device.");

        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (user.IsSuspended)
            throw new UnauthorizedAccessException("Account is suspended.");

        // Revoke old refresh token and generate new one
        var roles = await GetUserRolesAsync(user, cancellationToken);
        var permissions = await GetUserPermissionsAsync(user, cancellationToken);
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles, permissions);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        session.RotateRefreshToken(
            HashToken(newRefreshToken),
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Token refresh
        await _auditService.RecordAsync(
            "TokenRefreshed",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            cancellationToken: cancellationToken);

        return new AuthResponse(
            accessToken,
            newRefreshToken,
            expiresAt,
            MapToUserInfo(user, roles, permissions)
        );
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var session = await _userRepository.GetSessionByRefreshTokenHashAsync(
            HashToken(refreshToken), cancellationToken);
        if (session == null || session.UserId != userId) return;
        session.Revoke();
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: User logout
        await _auditService.RecordAsync(
            "UserLogout",
            "User",
            userId.ToString(),
            user?.TenantId,
            userId,
            cancellationToken: cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        user.ChangePassword(request.CurrentPassword, request.NewPassword);
        user.RevokeAllSessions();
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Password changed
        await _auditService.RecordAsync(
            "PasswordChanged",
            "User",
            userId.ToString(),
            user.TenantId,
            userId,
            cancellationToken: cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
            return; // Don't reveal whether the email exists

        var now = DateTime.UtcNow;
        await _userRepository.RevokeActionTokensAsync(
            user.Id, IdentityActionTokenPurpose.PasswordReset, now, cancellationToken);
        var resetToken = _tokenService.GenerateRefreshToken();
        await _userRepository.AddActionTokenAsync(new IdentityActionToken(
            Guid.NewGuid(),
            user.Id,
            IdentityActionTokenPurpose.PasswordReset,
            HashToken(resetToken),
            now.AddHours(1)), cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Password reset requested
        await _auditService.RecordAsync(
            "PasswordResetRequested",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            cancellationToken: cancellationToken);

        await _messageSender.SendPasswordResetAsync(user.Email, resetToken, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var token = await _userRepository.GetActionTokenByHashAsync(
            HashToken(request.Token), IdentityActionTokenPurpose.PasswordReset, cancellationToken);
        if (token == null || !token.IsUsable(DateTime.UtcNow))
            throw new InvalidOperationException("Invalid reset request.");
        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user == null || !string.Equals(
                user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid reset request.");

        token.Consume(DateTime.UtcNow);
        user.ResetPassword(request.NewPassword);
        user.RevokeAllSessions();
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Password reset completed
        await _auditService.RecordAsync(
            "PasswordResetCompleted",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            cancellationToken: cancellationToken);
    }

    public async Task RequestEmailVerificationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        if (user.EmailVerified)
            throw new InvalidOperationException("Email is already verified.");

        var now = DateTime.UtcNow;
        await _userRepository.RevokeActionTokensAsync(
            user.Id, IdentityActionTokenPurpose.EmailVerification, now, cancellationToken);
        var verificationToken = _tokenService.GenerateRefreshToken();
        await _userRepository.AddActionTokenAsync(new IdentityActionToken(
            Guid.NewGuid(),
            user.Id,
            IdentityActionTokenPurpose.EmailVerification,
            HashToken(verificationToken),
            now.AddHours(24)), cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Email verification requested
        await _auditService.RecordAsync(
            "EmailVerificationRequested",
            "User",
            userId.ToString(),
            user.TenantId,
            userId,
            cancellationToken: cancellationToken);

        await _messageSender.SendEmailVerificationAsync(
            user.Email, verificationToken, cancellationToken);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var actionToken = await _userRepository.GetActionTokenByHashAsync(
            HashToken(token), IdentityActionTokenPurpose.EmailVerification, cancellationToken);
        if (actionToken == null || !actionToken.IsUsable(DateTime.UtcNow))
            throw new InvalidOperationException("Invalid verification token.");
        var user = await _userRepository.GetByIdAsync(actionToken.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("Invalid verification token.");

        actionToken.Consume(DateTime.UtcNow);
        user.VerifyEmail();
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Audit: Email verified
        await _auditService.RecordAsync(
            "EmailVerified",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            cancellationToken: cancellationToken);
    }

    public async Task<UserInfo> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        var roles = await GetUserRolesAsync(user, cancellationToken);
        var permissions = await GetUserPermissionsAsync(user, cancellationToken);

        return MapToUserInfo(user, roles, permissions);
    }

    public async Task<IReadOnlyList<DeviceSessionResponse>> GetDeviceSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
        return user.Sessions
            .OrderByDescending(session => session.LastActivityAt)
            .Select(session => new DeviceSessionResponse(
                session.Id,
                session.DeviceId,
                session.DeviceName,
                session.DeviceType,
                session.IpAddress,
                session.CreatedAt,
                session.LastActivityAt,
                session.RefreshTokenExpiry,
                session.IsRevoked,
                session.RevokedAt))
            .ToArray();
    }

    public async Task RevokeDeviceSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
        var session = user.Sessions.SingleOrDefault(item => item.Id == sessionId)
            ?? throw new KeyNotFoundException("Device session not found.");
        session.Revoke();
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        await _auditService.RecordAsync(
            "DeviceSessionRevoked", "DeviceSession", sessionId.ToString(),
            user.TenantId, userId, reason: "User revoked a device session",
            cancellationToken: cancellationToken);
    }

    public Task SuspendUserAsync(
        Guid actorId,
        Guid tenantId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SetSuspensionAsync(actorId, tenantId, userId, reason, true, cancellationToken);

    public Task UnsuspendUserAsync(
        Guid actorId,
        Guid tenantId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SetSuspensionAsync(actorId, tenantId, userId, reason, false, cancellationToken);

    private async Task SetSuspensionAsync(
        Guid actorId,
        Guid tenantId,
        Guid userId,
        string reason,
        bool suspend,
        CancellationToken cancellationToken)
    {
        if (actorId == userId && suspend)
            throw new InvalidOperationException("You cannot suspend your own account.");
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");
        if (user.TenantId != tenantId)
            throw new KeyNotFoundException("User not found.");
        if (suspend) user.Suspend(reason); else user.Unsuspend();
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        await _auditService.RecordAsync(
            suspend ? "UserSuspended" : "UserUnsuspended",
            "User",
            user.Id.ToString(),
            tenantId,
            actorId,
            suspend ? "{\"isSuspended\":false}" : "{\"isSuspended\":true}",
            suspend ? "{\"isSuspended\":true}" : "{\"isSuspended\":false}",
            reason,
            cancellationToken);
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersByTenantAsync(Guid tenantId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetByTenantAsync(tenantId, page, pageSize, cancellationToken);
        var result = new List<UserResponse>();

        foreach (var user in users)
        {
            var roles = await GetUserRolesAsync(user, cancellationToken);
            result.Add(new UserResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.EmailVerified,
                user.IsActive,
                user.IsSuspended,
                user.LastLoginAt,
                roles,
                user.TenantId
            ));
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> GetUserRolesAsync(User user, CancellationToken cancellationToken)
    {
        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = new List<string>();

        foreach (var roleId in roleIds)
        {
            var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
            if (role != null)
            {
                roles.Add(role.Name);
            }
        }

        return roles;
    }

    private async Task<IReadOnlyList<string>> GetUserPermissionsAsync(User user, CancellationToken cancellationToken)
    {
        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var permissions = new List<string>();

        foreach (var roleId in roleIds)
        {
            var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
            if (role != null)
            {
                permissions.AddRange(role.Permissions.Select(p => p.Permission));
            }
        }

        return permissions.Distinct().ToList();
    }

    private static UserInfo MapToUserInfo(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.EmailVerified,
            roles,
            permissions,
            user.TenantId
        );
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
