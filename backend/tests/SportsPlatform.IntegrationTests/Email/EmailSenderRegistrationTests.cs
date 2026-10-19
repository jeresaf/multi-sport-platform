using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Platform.Infrastructure.Email;

namespace SportsPlatform.IntegrationTests.Email;

/// <summary>The Host sends through the SMTP adapter and checks its settings on start; tests never send for real.</summary>
public sealed class EmailSenderRegistrationTests
{
    [Fact]
    public void The_Host_sends_through_the_SMTP_adapter()
    {
        using var app = new HostOnlyApp();

        app.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<SmtpEmailSender>();
    }

    // Staging has no Email settings of its own: the API must not start without them.
    [Fact]
    public void The_API_does_not_start_without_email_settings()
    {
        using var app = new StagingApp();

        var exception = Should.Throw<OptionsValidationException>(() => app.Services);

        exception.OptionsType.ShouldBe(typeof(EmailOptions));
        exception.Failures.ShouldContain(failure => failure.StartsWith("Email:From", StringComparison.Ordinal));
        exception.Failures.ShouldContain(failure => failure.StartsWith("Email:Smtp:Host", StringComparison.Ordinal));
    }

    private sealed class StagingApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            // Tenancy refuses Staging without a real platform domain; give it one so the email check is reached.
            builder.UseSetting("Platform:BaseDomain", "sportsplatform.example");
        }
    }
}
