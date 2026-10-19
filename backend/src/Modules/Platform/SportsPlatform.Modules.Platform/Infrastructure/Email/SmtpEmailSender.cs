using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// Sends through an SMTP server with MailKit: the only provider-specific email code (S03 D13, AR-33). One connection
/// per email, which is plenty for transactional volumes. Failures throw <see cref="EmailDeliveryException"/>, so the
/// outbox retries the consumer; a server without STARTTLS when TLS is required throws <see cref="NotSupportedException"/>
/// (a configuration error). Logs and errors carry the Message-ID only, never an address or the content (ADR-0015).
/// </summary>
public sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mime = Build(message, settings);

        try
        {
            using var client = new SmtpClient { Timeout = settings.Smtp.TimeoutSeconds * 1000 };
            await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, ToSocketOptions(settings.Smtp.Security), cancellationToken);
            if (!string.IsNullOrEmpty(settings.Smtp.UserName))
            {
                await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password ?? "", cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception exception) when (EmailDeliveryException.IsTransportFailure(exception))
        {
            throw EmailDeliveryException.From(exception, mime.MessageId);
        }

        LogSent(mime.MessageId);
    }

    // Callers pass addresses Identity has validated; if one still doesn't parse, MimeKit's error must not reach the
    // outbox's log or last_error either.
    private static MimeMessage Build(EmailMessage message, EmailOptions settings)
    {
        try
        {
            return EmailMimeBuilder.Build(message, settings);
        }
        catch (ParseException exception)
        {
            throw EmailDeliveryException.From(exception, messageId: null);
        }
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => throw new ArgumentOutOfRangeException(nameof(security), security, "Unknown SMTP security."),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Email {MessageId} sent")]
    private partial void LogSent(string? messageId);
}
