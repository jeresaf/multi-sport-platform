using System.Collections.Concurrent;
using SportsPlatform.Modules.Identity.Services;

namespace SportsPlatform.IntegrationTests;

public sealed class TestIdentityMessageSender : IIdentityMessageSender
{
    private readonly ConcurrentQueue<IdentityMessage> _messages = new();

    public IReadOnlyCollection<IdentityMessage> Messages => _messages.ToArray();

    public Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new IdentityMessage(email, token, "PasswordReset"));
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new IdentityMessage(email, token, "EmailVerification"));
        return Task.CompletedTask;
    }

    public IdentityMessage Single(string email, string purpose) =>
        _messages.Last(message => message.Email == email && message.Purpose == purpose);
}

public sealed record IdentityMessage(string Email, string Token, string Purpose);
