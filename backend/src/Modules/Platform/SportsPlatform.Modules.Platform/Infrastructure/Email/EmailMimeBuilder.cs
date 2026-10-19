using MimeKit;
using MimeKit.Utils;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// Builds the MIME message: plain text alone, or plain text and HTML as alternatives, with a Message-ID on the
/// sender's domain (the only thing the sender logs).
/// </summary>
public static class EmailMimeBuilder
{
    public static MimeMessage Build(EmailMessage message, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        var from = new MailboxAddress(options.FromName, options.From);
        var mime = new MimeMessage { MessageId = MimeUtils.GenerateMessageId(from.Domain) };
        mime.From.Add(from);
        mime.To.Add(new MailboxAddress(message.ToName ?? "", message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();
        return mime;
    }
}
