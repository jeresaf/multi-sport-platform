namespace SportsPlatform.Modules.Identity.Infrastructure;

/// <summary>
/// The account settings (<c>Identity:*</c>): where emailed links point and the sign-in and password-reset rate limits
/// (S03 D15). Validated on start (<see cref="AccountOptionsValidator"/>).
/// </summary>
public sealed class AccountOptions
{
    public const string SectionName = "Identity";

    /// <summary>The admin web's address, e.g. <c>http://localhost:3001</c>: reset links point at its pages.</summary>
    public string AdminAppUrl { get; set; } = "";

    public AccountRateLimitOptions RateLimits { get; set; } = new();
}

/// <summary>
/// <c>Identity:RateLimits:*</c>: per client IP (checked before the audit, so a flood writes no audit rows) and per
/// normalised email (checked after the request is read; refusals are audited). In memory, per API instance.
/// </summary>
public sealed class AccountRateLimitOptions
{
    public RateLimitWindow LoginPerIp { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    public RateLimitWindow LoginPerEmail { get; set; } = new() { PermitLimit = 10, WindowSeconds = 15 * 60 };

    public RateLimitWindow ForgotPasswordPerIp { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 * 60 };

    public RateLimitWindow ForgotPasswordPerEmail { get; set; } = new() { PermitLimit = 3, WindowSeconds = 60 * 60 };

    /// <summary>Invitation lookup and acceptance together (S03-T4c2, owner): 30 per 10 minutes.</summary>
    public RateLimitWindow InvitationPerIp { get; set; } = new() { PermitLimit = 30, WindowSeconds = 10 * 60 };
}

/// <summary>At most <see cref="PermitLimit"/> requests per fixed window of <see cref="WindowSeconds"/>.</summary>
public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
