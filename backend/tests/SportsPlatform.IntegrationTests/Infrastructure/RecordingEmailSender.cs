using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="TestApp"/>'s <see cref="IEmailSender"/>: keeps what would have been sent, so no test opens an SMTP
/// connection. The real adapter is tested against Mailpit (<c>Email/SmtpEmailSenderTests</c>).
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly Lock _lock = new();
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_lock)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_lock)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _sent.Clear();
        }
    }
}
