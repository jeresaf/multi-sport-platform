using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

/// <summary>
/// The per-email limits (S03 D15), keyed by the normalised email (trimmed, upper case, as Identity looks emails up), so
/// "Grace@…" and " grace@… " share one limit. Holds no email beyond the window; idle partitions are dropped.
/// </summary>
public sealed class EmailRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _login;
    private readonly PartitionedRateLimiter<string> _forgotPassword;

    public EmailRateLimiter(IOptions<AccountOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Value.RateLimits;
        _login = Create(limits.LoginPerEmail);
        _forgotPassword = Create(limits.ForgotPasswordPerEmail);
    }

    /// <summary>Takes one permit for <paramref name="email"/>; dispose the lease, and refuse when it isn't acquired.</summary>
    public RateLimitLease Acquire(AccountRateLimit limit, string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var limiter = limit switch
        {
            AccountRateLimit.Login => _login,
            AccountRateLimit.ForgotPassword => _forgotPassword,
            _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, null),
        };
        return limiter.AttemptAcquire(Normalize(email));
    }

    /// <summary>The same normalisation as Identity's <c>UpperInvariantLookupNormalizer</c>, after trimming.</summary>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().Normalize().ToUpperInvariant();
    }

    /// <summary>A fixed window without a queue: a request over the limit is refused at once.</summary>
    internal static FixedWindowRateLimiterOptions FixedWindow(RateLimitWindow window) => new()
    {
        PermitLimit = window.PermitLimit,
        Window = window.Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    public void Dispose()
    {
        _login.Dispose();
        _forgotPassword.Dispose();
    }

    private static PartitionedRateLimiter<string> Create(RateLimitWindow window) =>
        PartitionedRateLimiter.Create<string, string>(email => RateLimitPartition.GetFixedWindowLimiter(email, _ => FixedWindow(window)));
}
