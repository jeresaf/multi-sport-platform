using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Platform.Infrastructure.Email;

namespace SportsPlatform.UnitTests.Platform;

public sealed class EmailDeliveryExceptionTests
{
    private const string _messageId = "01a15412c1917000@sportsplatform.localhost";

    [Fact]
    public void Keeps_the_SMTP_codes_and_the_message_ID_but_not_the_server_text()
    {
        var rejected = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            new MailboxAddress("Esther Nabirye", "esther.nabirye@kmsa.example"),
            "5.1.1 <esther.nabirye@kmsa.example>: Recipient address rejected");

        var exception = EmailDeliveryException.From(rejected, _messageId);

        exception.Message.ShouldContain(_messageId);
        exception.Message.ShouldContain("550");
        exception.Message.ShouldContain(nameof(SmtpErrorCode.RecipientNotAccepted));
        exception.ToString().ShouldNotContain("esther.nabirye", Case.Insensitive);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Describes_a_network_failure_by_its_error_code()
    {
        var exception = EmailDeliveryException.From(new SocketException((int)SocketError.ConnectionRefused), _messageId);

        exception.Message.ShouldContain(nameof(SocketError.ConnectionRefused));
    }

    [Fact]
    public async Task An_address_that_does_not_parse_fails_before_connecting_without_the_address_in_the_error()
    {
        var sender = new SmtpEmailSender(
            Options.Create(new EmailOptions { From = "no-reply@sportsplatform.localhost", Smtp = new SmtpOptions { Host = "smtp.invalid" } }),
            NullLogger<SmtpEmailSender>.Instance);
        var message = new EmailMessage("daniel.kato at kmsa.example", "Daniel Kato", "Your KMSA invitation", "Welcome.");

        var exception = await Should.ThrowAsync<EmailDeliveryException>(() => sender.SendAsync(message, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Email not delivered: ParseException.");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Wraps_transport_failures_only()
    {
        EmailDeliveryException.IsTransportFailure(new AuthenticationException("535 5.7.8 rejected")).ShouldBeTrue();
        EmailDeliveryException.IsTransportFailure(new IOException("connection reset")).ShouldBeTrue();
        EmailDeliveryException.IsTransportFailure(new SocketException()).ShouldBeTrue();

        // A missing STARTTLS is a configuration error, and a bug is a bug: both stay as they are.
        EmailDeliveryException.IsTransportFailure(new NotSupportedException()).ShouldBeFalse();
        EmailDeliveryException.IsTransportFailure(new InvalidOperationException()).ShouldBeFalse();
        EmailDeliveryException.IsTransportFailure(new OperationCanceledException()).ShouldBeFalse();
    }
}
