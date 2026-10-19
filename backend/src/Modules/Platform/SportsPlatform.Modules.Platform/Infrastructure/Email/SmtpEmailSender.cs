using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// Sends through an SMTP server with MailKit: the only provider-specific email code (S03 D13, AR-33). One connection
/// per email, which is plenty for transactional volumes. Failures throw, so the outbox retries the consumer. Logs
/// carry the Message-ID only, never an address or the content (ADR-0015).
/// </summary>
public sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mime = EmailMimeBuilder.Build(message, settings);

        using var client = new SmtpClient { Timeout = settings.Smtp.TimeoutSeconds * 1000 };
        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, ToSocketOptions(settings.Smtp.Security), cancellationToken);
        if (!string.IsNullOrEmpty(settings.Smtp.UserName))
        {
            await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password ?? "", cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
        LogSent(mime.MessageId);
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
