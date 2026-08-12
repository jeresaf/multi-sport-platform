using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Identity.DTOs;
using SportsPlatform.Modules.Identity.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditService _audit;
    private readonly SportsPlatformDbContext _database;
    private readonly IMfaService _mfaService;

    public AuthController(
        IAuthService authService,
        IAuditService audit,
        SportsPlatformDbContext database,
        IMfaService mfaService)
    {
        _authService = authService;
        _audit = audit;
        _database = database;
        _mfaService = mfaService;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("authentication")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            await RecordAuthAsync(
                "AuthenticationRegistered", response.User.Id, response.User.TenantId,
                "User registered", cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("authentication")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            await RecordAuthAsync(
                "AuthenticationLoginSucceeded", response.User.Id, response.User.TenantId,
                $"Login succeeded for device {request.DeviceId ?? "unknown"}", cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            var failedUser = await _database.Users.AsNoTracking().FirstOrDefaultAsync(
                item => item.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);
            await RecordAuthAsync(
                "AuthenticationLoginFailed",
                failedUser?.Id,
                failedUser?.TenantId,
                ex.Message,
                cancellationToken,
                request.Email.Trim().ToLower());
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("authentication")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request, cancellationToken);
            await RecordAuthAsync(
                "AuthenticationTokenRefreshed", response.User.Id, response.User.TenantId,
                $"Access token refreshed for device {request.DeviceId ?? "unknown"}", cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            await RecordAuthAsync(
                "AuthenticationTokenRefreshFailed", null, null, ex.Message,
                cancellationToken, request.DeviceId ?? "unknown-device");
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Logout and revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _authService.LogoutAsync(userId, request.RefreshToken, cancellationToken);
        await RecordAuthAsync(
            "AuthenticationLoggedOut", userId, User.TenantId(), "User logged out", cancellationToken);
        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            await _authService.ChangePasswordAsync(userId, request, cancellationToken);
            await RecordAuthAsync(
                "AuthenticationPasswordChanged", userId, User.TenantId(),
                "Password changed by authenticated user", cancellationToken);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request password reset (forgot password)
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-recovery")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        var resetUser = await _database.Users.AsNoTracking().FirstOrDefaultAsync(
            item => item.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);
        if (resetUser is not null)
            await RecordAuthAsync(
                "AuthenticationPasswordResetRequested", resetUser.Id, resetUser.TenantId,
                "Password reset requested", cancellationToken);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    /// <summary>
    /// Reset password using reset token
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("password-recovery")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ResetPasswordAsync(request, cancellationToken);
            var resetUser = await _database.Users.AsNoTracking().FirstOrDefaultAsync(
                item => item.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);
            await RecordAuthAsync(
                "AuthenticationPasswordReset", resetUser?.Id, resetUser?.TenantId,
                "Password reset completed", cancellationToken,
                resetUser?.Id.ToString() ?? request.Email.Trim().ToLower());
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request email verification
    /// </summary>
    [HttpPost("request-email-verification")]
    [Authorize]
    public async Task<ActionResult> RequestEmailVerification(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            await _authService.RequestEmailVerificationAsync(userId, cancellationToken);
            await RecordAuthAsync(
                "AuthenticationEmailVerificationRequested", userId, User.TenantId(),
                "Email verification requested", cancellationToken);
            return Ok(new { message = "Verification email sent." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Verify email with token
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _authService.VerifyEmailAsync(request.Token, cancellationToken);
            return Ok(new { message = "Email verified successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(user);
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<DeviceSessionResponse>>> GetDeviceSessions(
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetDeviceSessionsAsync(GetUserId(), cancellationToken));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<ActionResult> RevokeDeviceSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeDeviceSessionAsync(GetUserId(), sessionId, cancellationToken);
        return Ok(new { message = "Device session revoked successfully." });
    }

    [HttpPost("mfa/enroll")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult> EnrollMfa(CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync(new object[] { GetUserId() }, cancellationToken);
        if (user is null) return NotFound();
        var secret = _mfaService.BeginEnrollment(user);
        await _database.SaveChangesAsync(cancellationToken);
        var issuer = Uri.EscapeDataString("Sports Platform");
        var account = Uri.EscapeDataString(user.Email);
        var uri = $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits=6&period=30";
        await RecordAuthAsync("AuthenticationMfaEnrollmentStarted", user.Id, user.TenantId,
            "MFA enrollment started", cancellationToken);
        return Ok(new { secret, otpauthUri = uri });
    }

    [HttpPost("mfa/verify")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult> VerifyMfa([FromBody] MfaCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync(new object[] { GetUserId() }, cancellationToken);
        if (user is null) return NotFound();
        if (!_mfaService.VerifyCode(user, request.Code))
            return BadRequest(new { error = "The MFA code is invalid." });
        user.EnableMfa();
        await _database.SaveChangesAsync(cancellationToken);
        await RecordAuthAsync("AuthenticationMfaEnabled", user.Id, user.TenantId,
            "MFA enabled", cancellationToken);
        return Ok(new { message = "MFA enabled successfully." });
    }

    [HttpDelete("mfa")]
    [Authorize]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult> DisableMfa([FromBody] MfaCodeRequest request, CancellationToken cancellationToken)
    {
        var user = await _database.Users.FindAsync(new object[] { GetUserId() }, cancellationToken);
        if (user is null) return NotFound();
        if (!user.MfaEnabled || !_mfaService.VerifyCode(user, request.Code))
            return BadRequest(new { error = "The MFA code is invalid." });
        user.DisableMfa();
        await _database.SaveChangesAsync(cancellationToken);
        await RecordAuthAsync("AuthenticationMfaDisabled", user.Id, user.TenantId,
            "MFA disabled", cancellationToken);
        return Ok(new { message = "MFA disabled successfully." });
    }

    /// <summary>
    /// Get users by tenant (tenant admin)
    /// </summary>
    [HttpGet("users")]
    [Authorize]
    [RequirePermission("users.read")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetUsersByTenant(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (!tenantId.HasValue)
            return BadRequest(new { error = "Tenant context required." });

        var users = await _authService.GetUsersByTenantAsync(tenantId.Value, page, pageSize, cancellationToken);
        return Ok(users);
    }

    [HttpPost("users/{userId:guid}/suspend")]
    [Authorize]
    [RequirePermission("users.manage")]
    public async Task<ActionResult> SuspendUser(
        Guid userId,
        [FromBody] AccountSuspensionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (!tenantId.HasValue)
            return BadRequest(new { error = "Tenant context required." });
        await _authService.SuspendUserAsync(
            GetUserId(), tenantId.Value, userId, request.Reason, cancellationToken);
        return Ok(new { message = "User suspended successfully." });
    }

    [HttpPost("users/{userId:guid}/unsuspend")]
    [Authorize]
    [RequirePermission("users.manage")]
    public async Task<ActionResult> UnsuspendUser(
        Guid userId,
        [FromBody] AccountSuspensionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (!tenantId.HasValue)
            return BadRequest(new { error = "Tenant context required." });
        await _authService.UnsuspendUserAsync(
            GetUserId(), tenantId.Value, userId, request.Reason, cancellationToken);
        return Ok(new { message = "User unsuspended successfully." });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }

    private Guid? GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim != null ? Guid.Parse(claim.Value) : null;
    }

    private Task RecordAuthAsync(
        string action,
        Guid? userId,
        Guid? tenantId,
        string reason,
        CancellationToken cancellationToken,
        string? target = null) => _audit.RecordAsync(
            action,
            "Authentication",
            target ?? userId?.ToString() ?? "anonymous",
            tenantId,
            userId,
            null,
            null,
            reason,
            cancellationToken);
}
