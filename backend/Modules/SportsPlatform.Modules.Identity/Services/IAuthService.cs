using SportsPlatform.Modules.Identity.DTOs;

namespace SportsPlatform.Modules.Identity.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task RequestEmailVerificationAsync(Guid userId, CancellationToken cancellationToken = default);
    Task VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceSessionResponse>> GetDeviceSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeDeviceSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task SuspendUserAsync(Guid actorId, Guid tenantId, Guid userId, string reason, CancellationToken cancellationToken = default);
    Task UnsuspendUserAsync(Guid actorId, Guid tenantId, Guid userId, string reason, CancellationToken cancellationToken = default);
    Task<UserInfo> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserResponse>> GetUsersByTenantAsync(Guid tenantId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
