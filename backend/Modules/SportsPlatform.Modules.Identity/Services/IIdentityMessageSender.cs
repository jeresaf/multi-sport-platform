namespace SportsPlatform.Modules.Identity.Services;

public interface IIdentityMessageSender
{
    Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);

    Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);
}
