using System.ComponentModel.DataAnnotations;

namespace SportsPlatform.Modules.Identity.DTOs;

public record RegisterRequest(
    [param: Required, EmailAddress, StringLength(254)]
    string Email,
    [param: Required, StringLength(128, MinimumLength = 8)]
    string Password,
    [param: Required, StringLength(100)]
    string FirstName,
    [param: Required, StringLength(100)]
    string LastName,
    [param: Phone, StringLength(30)]
    string? PhoneNumber,
    Guid? TenantId = null,
    [param: StringLength(200)]
    string? DeviceId = null,
    [param: StringLength(200)]
    string? DeviceName = null,
    [param: StringLength(50)]
    string? DeviceType = null
);

public record LoginRequest(
    [param: Required, EmailAddress, StringLength(254)]
    string Email,
    [param: Required, StringLength(128, MinimumLength = 8)]
    string Password,
    [param: StringLength(200)]
    string? DeviceId,
    [param: StringLength(200)]
    string? DeviceName,
    [param: StringLength(50)]
    string? DeviceType,
    [param: StringLength(50)]
    string? IpAddress,
    string? MfaCode = null
);

public record MfaCodeRequest(
    string Code
);

public record RefreshTokenRequest(
    [param: Required, StringLength(512)]
    string RefreshToken,
    [param: StringLength(200)]
    string? DeviceId
);

public record ChangePasswordRequest(
    [param: Required, StringLength(128, MinimumLength = 8)]
    string CurrentPassword,
    [param: Required, StringLength(128, MinimumLength = 8)]
    string NewPassword
);

public record ForgotPasswordRequest(
    [param: Required, EmailAddress, StringLength(254)]
    string Email
);

public record ResetPasswordRequest(
    [param: Required, EmailAddress, StringLength(254)]
    string Email,
    [param: Required, StringLength(512)]
    string Token,
    [param: Required, StringLength(128, MinimumLength = 8)]
    string NewPassword
);

public record VerifyEmailRequest(
    [param: Required, StringLength(512)]
    string Token
);

public record AccountSuspensionRequest(
    [param: Required, StringLength(1000, MinimumLength = 3)]
    string Reason
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserInfo User
);

public record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool EmailVerified,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? TenantId
);

public record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions,
    Guid? TenantId
);

public record CreateRoleRequest(
    [param: Required, StringLength(100, MinimumLength = 2)]
    string Name,
    [param: StringLength(500)]
    string? Description,
    List<string>? Permissions
);

public record UpdateRoleRequest(
    [param: Required, StringLength(100, MinimumLength = 2)]
    string Name,
    [param: StringLength(500)]
    string? Description,
    List<string>? Permissions
);

public record AssignRoleRequest(
    Guid UserId,
    Guid RoleId
);

public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool EmailVerified,
    bool IsActive,
    bool IsSuspended,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    Guid? TenantId
);

public record DeviceSessionResponse(
    Guid Id,
    string DeviceId,
    string DeviceName,
    string? DeviceType,
    string? IpAddress,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    DateTime RefreshTokenExpiry,
    bool IsRevoked,
    DateTime? RevokedAt
);
