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
    private bool _failNext;

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
            if (_failNext)
            {
                _failNext = false;
                throw new InvalidOperationException("The test SMTP server refused the message.");
            }

            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>The next send fails, as a transport failure would: nothing is recorded and the outbox retries.</summary>
    public void FailNext()
    {
        lock (_lock)
        {
            _failNext = true;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _sent.Clear();
            _failNext = false;
        }
    }
}
