using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

namespace SportsPlatform.UnitTests.Identity;

public sealed class AccountRateLimitTests
{
    [Fact]
    public void One_email_in_any_case_or_spacing_shares_one_limit()
    {
        using var limiter = Limiter(loginPerEmail: 2);

        using var first = limiter.Acquire(AccountRateLimit.Login, "grace.nakato@kmsa.example");
        using var second = limiter.Acquire(AccountRateLimit.Login, "  Grace.Nakato@KMSA.example ");
        using var third = limiter.Acquire(AccountRateLimit.Login, "GRACE.NAKATO@KMSA.EXAMPLE");

        first.IsAcquired.ShouldBeTrue();
        second.IsAcquired.ShouldBeTrue();
        third.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public void Emails_and_endpoints_have_separate_limits()
    {
        using var limiter = Limiter(loginPerEmail: 1);

        using var grace = limiter.Acquire(AccountRateLimit.Login, "grace.nakato@kmsa.example");
        using var daniel = limiter.Acquire(AccountRateLimit.Login, "daniel.kato@kmsa.example");
        using var forgot = limiter.Acquire(AccountRateLimit.ForgotPassword, "grace.nakato@kmsa.example");

        grace.IsAcquired.ShouldBeTrue();
        daniel.IsAcquired.ShouldBeTrue();
        forgot.IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void The_defaults_are_the_agreed_limits()
    {
        var limits = new AccountOptions().RateLimits;

        (limits.LoginPerIp.PermitLimit, limits.LoginPerIp.Window).ShouldBe((20, TimeSpan.FromMinutes(1)));
        (limits.LoginPerEmail.PermitLimit, limits.LoginPerEmail.Window).ShouldBe((10, TimeSpan.FromMinutes(15)));
        (limits.ForgotPasswordPerIp.PermitLimit, limits.ForgotPasswordPerIp.Window).ShouldBe((10, TimeSpan.FromHours(1)));
        (limits.ForgotPasswordPerEmail.PermitLimit, limits.ForgotPasswordPerEmail.Window).ShouldBe((3, TimeSpan.FromHours(1)));
    }

    private static EmailRateLimiter Limiter(int loginPerEmail)
    {
        var options = new AccountOptions();
        options.RateLimits.LoginPerEmail = new RateLimitWindow { PermitLimit = loginPerEmail, WindowSeconds = 900 };
        return new EmailRateLimiter(Options.Create(options));
    }
}
