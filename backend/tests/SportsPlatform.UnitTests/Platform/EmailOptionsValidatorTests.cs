using SportsPlatform.Modules.Platform.Infrastructure.Email;

namespace SportsPlatform.UnitTests.Platform;

public sealed class EmailOptionsValidatorTests
{
    private static readonly EmailOptionsValidator _validator = new();

    [Fact]
    public void Accepts_Mailpit_and_an_authenticated_TLS_server()
    {
        _validator.Validate(null, Valid()).Succeeded.ShouldBeTrue();
        _validator.Validate(null, Valid(o =>
        {
            o.Smtp = new SmtpOptions { Host = "smtp.example.org", Port = 465, Security = SmtpSecurity.SslOnConnect, UserName = "msp", Password = "from-a-secret-store" };
        })).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-reply")]
    [InlineData("SportsPlatform <no-reply@sportsplatform.localhost>")]
    [InlineData("a@example.org, b@example.org")]
    public void Refuses_a_sender_that_is_not_one_address(string from) =>
        FailureFor(Valid(o => o.From = from)).ShouldStartWith("Email:From");

    [Fact]
    public void Refuses_an_empty_sender_name() =>
        FailureFor(Valid(o => o.FromName = " ")).ShouldStartWith("Email:FromName");

    [Fact]
    public void Refuses_a_missing_host() =>
        FailureFor(Valid(o => o.Smtp.Host = "")).ShouldStartWith("Email:Smtp:Host");

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Refuses_a_port_out_of_range(int port) =>
        FailureFor(Valid(o => o.Smtp.Port = port)).ShouldStartWith("Email:Smtp:Port");

    [Fact]
    public void Refuses_an_unknown_security_mode() =>
        FailureFor(Valid(o => o.Smtp.Security = (SmtpSecurity)7)).ShouldStartWith("Email:Smtp:Security");

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void Refuses_a_timeout_out_of_range(int seconds) =>
        FailureFor(Valid(o => o.Smtp.TimeoutSeconds = seconds)).ShouldStartWith("Email:Smtp:TimeoutSeconds");

    [Theory]
    [InlineData("msp", null)]
    [InlineData(null, "from-a-secret-store")]
    public void Refuses_half_a_credential(string? userName, string? password) =>
        FailureFor(Valid(o => (o.Smtp.UserName, o.Smtp.Password) = (userName, password))).ShouldStartWith("Email:Smtp:UserName");

    [Fact]
    public void Never_echoes_the_configured_values()
    {
        var result = _validator.Validate(null, Valid(o => (o.From, o.Smtp.UserName) = ("not-an-address", "msp-user")));

        result.Failures.ShouldNotBeNull().ShouldAllBe(failure => !failure.Contains("not-an-address") && !failure.Contains("msp-user"));
    }

    private static string FailureFor(EmailOptions options)
    {
        var result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        return result.Failures.ShouldHaveSingleItem();
    }

    private static EmailOptions Valid(Action<EmailOptions>? change = null)
    {
        var options = new EmailOptions
        {
            From = "no-reply@sportsplatform.localhost",
            Smtp = new SmtpOptions { Host = "localhost", Port = 1025, Security = SmtpSecurity.None },
        };
        change?.Invoke(options);
        return options;
    }
}
