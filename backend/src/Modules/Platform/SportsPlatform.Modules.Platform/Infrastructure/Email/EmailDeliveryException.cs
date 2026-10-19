using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace SportsPlatform.Modules.Platform.Infrastructure.Email;

/// <summary>
/// A send that failed, described without the server's text: SMTP replies often repeat the recipient's address
/// (<c>5.1.1 &lt;…&gt;: Recipient address rejected</c>), and the outbox logs the exception and stores its message
/// (ADR-0015). The original exception is deliberately not kept as the inner exception, so its message and stack
/// trace never reach a log.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException()
    {
    }

    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The MailKit and network failures a send can hit; anything else is a bug and stays as it is.</summary>
    public static bool IsTransportFailure(Exception exception) => exception
        is SmtpCommandException or SmtpProtocolException or AuthenticationException or SslHandshakeException
        or ServiceNotConnectedException or ServiceNotAuthenticatedException or SocketException or IOException;

    /// <summary>Only the failure's type, the SMTP codes and the Message-ID: never the server's text.</summary>
    public static EmailDeliveryException From(Exception exception, string? messageId)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var smtp = exception is SmtpCommandException command
            ? $" (SMTP {(int)command.StatusCode} {command.StatusCode}, {command.ErrorCode})"
            : exception is SocketException socket ? $" ({socket.SocketErrorCode})" : "";
        return new EmailDeliveryException($"Email {messageId} not delivered: {exception.GetType().Name}{smtp}.");
    }
}
