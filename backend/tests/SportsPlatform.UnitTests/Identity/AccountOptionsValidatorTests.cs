using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.UnitTests.Identity;

public sealed class AccountOptionsValidatorTests
{
    [Theory]
    [InlineData("https://admin.kmsa.example")]
    [InlineData("http://localhost:3001/")]
    [InlineData("https://sportsplatform.example/admin")]
    public void A_usable_admin_address_passes(string url) =>
        new AccountOptionsValidator().Validate(null, new AccountOptions { AdminAppUrl = url }).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("admin.kmsa.example")]
    [InlineData("ftp://admin.kmsa.example")]
    [InlineData("https://admin.kmsa.example/?next=1")]
    public void The_admin_address_must_be_absolute_http_without_a_query(string url)
    {
        var result = new AccountOptionsValidator().Validate(null, new AccountOptions { AdminAppUrl = url });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldHaveSingleItem().ShouldStartWith("Identity:AdminAppUrl");
    }

    [Fact]
    public void A_rate_limit_needs_a_permit_and_a_window()
    {
        var options = new AccountOptions { AdminAppUrl = "https://admin.kmsa.example" };
        options.RateLimits.LoginPerIp = new RateLimitWindow { PermitLimit = 0, WindowSeconds = 60 };
        options.RateLimits.ForgotPasswordPerEmail = new RateLimitWindow { PermitLimit = 3, WindowSeconds = 0 };

        var result = new AccountOptionsValidator().Validate(null, options);

        result.Failures.ShouldNotBeNull().ShouldBe(
            [
                "Identity:RateLimits:LoginPerIp needs PermitLimit of at least 1 and WindowSeconds between 1 and 86400.",
                "Identity:RateLimits:ForgotPasswordPerEmail needs PermitLimit of at least 1 and WindowSeconds between 1 and 86400.",
            ]);
    }
}
