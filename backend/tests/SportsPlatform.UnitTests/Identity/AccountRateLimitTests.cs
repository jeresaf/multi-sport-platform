using System.Net;
using Microsoft.AspNetCore.Http;
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

    // One IPv6 client usually holds a /64: every address in it shares one limit.
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:41:7::1", "2001:db8:41:7::/64")]
    [InlineData("2001:db8:41:7:a1b2:c3d4:e5f6:1", "2001:db8:41:7::/64")]
    [InlineData("2001:db8:41:8::1", "2001:db8:41:8::/64")]
    public void The_client_key_is_the_IPv4_address_or_the_IPv6_64_network(string address, string key)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        AccountRateLimits.ClientIp(context).ShouldBe(key);
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
