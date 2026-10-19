using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.UnitTests.Identity;

public sealed class AccountOptionsValidatorTests
{
    [Theory]
    [InlineData("https://admin.kmsa.example")]
    [InlineData("https://sportsplatform.example/admin")]
    public void A_usable_admin_address_passes(string url) =>
        new AccountOptionsValidator(Environment("Production")).Validate(null, new AccountOptions { AdminAppUrl = url }).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("admin.kmsa.example")]
    [InlineData("ftp://admin.kmsa.example")]
    [InlineData("https://admin.kmsa.example/?next=1")]
    public void The_admin_address_must_be_absolute_http_without_a_query(string url)
    {
        var result = new AccountOptionsValidator(Environment("Production")).Validate(null, new AccountOptions { AdminAppUrl = url });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldHaveSingleItem().ShouldStartWith("Identity:AdminAppUrl");
    }

    [Fact]
    public void A_rate_limit_needs_a_permit_and_a_window()
    {
        var options = new AccountOptions { AdminAppUrl = "https://admin.kmsa.example" };
        options.RateLimits.LoginPerIp = new RateLimitWindow { PermitLimit = 0, WindowSeconds = 60 };
        options.RateLimits.ForgotPasswordPerEmail = new RateLimitWindow { PermitLimit = 3, WindowSeconds = 0 };

        var result = new AccountOptionsValidator(Environment("Production")).Validate(null, options);

        result.Failures.ShouldNotBeNull().ShouldBe(
            [
                "Identity:RateLimits:LoginPerIp needs PermitLimit of at least 1 and WindowSeconds between 1 and 86400.",
                "Identity:RateLimits:ForgotPasswordPerEmail needs PermitLimit of at least 1 and WindowSeconds between 1 and 86400.",
            ]);
    }

    // Review M1: reset links carry the token, so plain http only where the site is local.
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public void Http_is_allowed_only_in_Development_and_Testing(string environment, bool allowed)
    {
        var result = new AccountOptionsValidator(Environment(environment)).Validate(null, new AccountOptions { AdminAppUrl = "http://localhost:3001/" });

        result.Succeeded.ShouldBe(allowed);
        if (!allowed)
        {
            result.Failures.ShouldHaveSingleItem().ShouldStartWith("Identity:AdminAppUrl must be https");
        }
    }

    private static TestHostEnvironment Environment(string name) => new() { EnvironmentName = name };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";

        public string ApplicationName { get; set; } = "SportsPlatform.Host";

        public string ContentRootPath { get; set; } = "";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
