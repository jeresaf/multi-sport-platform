using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

/// <summary>The account endpoints with rate limits (S03 D15).</summary>
public enum AccountRateLimit
{
    Login,
    ForgotPassword,
}

/// <summary>
/// Sign-in and forgot-password rate limits (S03 D15), with ASP.NET Core's limiter: per client IP as a named policy, run
/// by the Host's <c>UseRateLimiter</c> before the audit, so a flood from one address writes no audit rows; per
/// normalised email as an endpoint filter after the request is validated, so a refusal is audited as DENIED. Either
/// answers <c>429 AUTH_RATE_LIMITED</c> with <c>Retry-After</c>. Known and unknown emails are limited alike.
/// </summary>
public static class AccountRateLimits
{
    public static IServiceCollection AddAccountRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy<string, LoginClientIpPolicy>(PolicyName(AccountRateLimit.Login));
            options.AddPolicy<string, ForgotPasswordClientIpPolicy>(PolicyName(AccountRateLimit.ForgotPassword));
        });
        services.AddSingleton<EmailRateLimiter>();
        return services;
    }

    /// <summary>Limits the endpoint per client IP and per the email <paramref name="email"/> reads from the request.</summary>
    public static RouteHandlerBuilder RateLimited<TRequest>(
        this RouteHandlerBuilder builder, AccountRateLimit limit, Func<TRequest, string?> email)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(email);

        builder.RequireRateLimiting(PolicyName(limit));
        builder.AddEndpointFilter(async (context, next) =>
        {
            // Runs after ValidateRequest (added first), so the email is present and of a bounded length.
            if (context.Arguments.OfType<TRequest>().SingleOrDefault() is { } request && email(request) is { Length: > 0 } address)
            {
                using var lease = context.HttpContext.RequestServices.GetRequiredService<EmailRateLimiter>().Acquire(limit, address);
                if (!lease.IsAcquired)
                {
                    return Refuse(context.HttpContext.Response, lease);
                }
            }

            return await next(context);
        });
        return builder.ProducesErrors(IdentityErrors.RateLimited);
    }

    /// <summary>The per-IP policy's name, e.g. <c>identity.login</c>.</summary>
    public static string PolicyName(AccountRateLimit limit) => limit switch
    {
        AccountRateLimit.Login => "identity.login",
        AccountRateLimit.ForgotPassword => "identity.password_forgot",
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, null),
    };

    /// <summary>The client's address: the connection's, or a trusted proxy's <c>X-Forwarded-For</c> (S02 D7).</summary>
    public static string ClientIp(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Connection.RemoteIpAddress switch
        {
            null => "unknown",
            { IsIPv4MappedToIPv6: true } address => address.MapToIPv4().ToString(),
            IPAddress address => address.ToString(),
        };
    }

    private static ProblemHttpResult Refuse(HttpResponse response, RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return IdentityErrors.RateLimited.ToProblem();
    }

    private abstract class ClientIpPolicy(RateLimitWindow window) : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask> OnRejected { get; } =
            static async (context, _) => await Refuse(context.HttpContext.Response, context.Lease).ExecuteAsync(context.HttpContext);

        public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(httpContext), _ => EmailRateLimiter.FixedWindow(window));
    }

    private sealed class LoginClientIpPolicy(IOptions<AccountOptions> options)
        : ClientIpPolicy(options.Value.RateLimits.LoginPerIp);

    private sealed class ForgotPasswordClientIpPolicy(IOptions<AccountOptions> options)
        : ClientIpPolicy(options.Value.RateLimits.ForgotPasswordPerIp);
}
