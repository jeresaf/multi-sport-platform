namespace SportsPlatform.Modules.Platform.Contracts.Email;

/// <summary>
/// Sends one transactional email (docs/architecture/modules.md, Platform; S03 D13). Call it only from an integration
/// event consumer, never inside a request: a rolled-back request then sends nothing, and the outbox retries a failed
/// send. A failure to deliver to the mail server throws, so the consumer's message is retried; a retry may send the
/// email again, so the consumer must make a resend harmless (a new token, the newest wins).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
