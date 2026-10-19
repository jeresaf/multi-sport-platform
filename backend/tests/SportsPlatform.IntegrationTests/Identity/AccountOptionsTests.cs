using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary><c>Identity:*</c> is checked on start (S03-T2b): reset emails need the admin web's address.</summary>
public sealed class AccountOptionsTests
{
    // Staging has no Identity settings of its own: the API must not start without the admin web's address.
    [Fact]
    public void The_API_does_not_start_without_the_admin_web_address()
    {
        using var app = new StagingApp();

        var exception = Should.Throw<OptionsValidationException>(() => app.Services);

        exception.OptionsType.ShouldBe(typeof(AccountOptions));
        exception.Failures.ShouldHaveSingleItem().ShouldStartWith("Identity:AdminAppUrl");
    }

    private sealed class StagingApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            // Everything else Staging refuses to start without, so the Identity check is the one that fails.
            builder.UseSetting("Platform:BaseDomain", "sportsplatform.example");
            builder.UseSetting("Email:From", "no-reply@sportsplatform.example");
            builder.UseSetting("Email:Smtp:Host", "smtp.sportsplatform.example");
        }
    }
}
