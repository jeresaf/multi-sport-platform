using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SportsPlatform.Modules.Identity.Infrastructure;

/// <summary>
/// Refuses to start without a usable admin web address or with a rate limit that can't work. Outside Development and
/// Testing the address must be https: reset links carry the token.
/// </summary>
public sealed class AccountOptionsValidator(IHostEnvironment environment) : IValidateOptions<AccountOptions>
{
    private const string _testingEnvironment = "Testing";

    public ValidateOptionsResult Validate(string? name, AccountOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!Uri.TryCreate(options.AdminAppUrl, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
        {
            failures.Add("Identity:AdminAppUrl must be an absolute http or https address without a query, e.g. https://admin.example.org.");
        }
        else if (url.Scheme != Uri.UriSchemeHttps && !environment.IsDevelopment() && !environment.IsEnvironment(_testingEnvironment))
        {
            failures.Add("Identity:AdminAppUrl must be https outside Development and Testing: reset links carry the token.");
        }

        var limits = options.RateLimits;
        Check(limits.LoginPerIp, "LoginPerIp");
        Check(limits.LoginPerEmail, "LoginPerEmail");
        Check(limits.ForgotPasswordPerIp, "ForgotPasswordPerIp");
        Check(limits.ForgotPasswordPerEmail, "ForgotPasswordPerEmail");
        Check(limits.InvitationPerIp, "InvitationPerIp");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        void Check(RateLimitWindow? window, string setting)
        {
            if (window is null || window.PermitLimit < 1 || window.WindowSeconds is < 1 or > 24 * 60 * 60)
            {
                failures.Add($"Identity:RateLimits:{setting} needs PermitLimit of at least 1 and WindowSeconds between 1 and 86400.");
            }
        }
    }
}
