using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Modules.Identity.Contracts.Account;

/// <summary>
/// Someone asked to reset a confirmed user's password (S03 D13, D15). The user ID only, never the email (ADR-0015):
/// Identity's own consumer loads the user, creates the reset token and sends the email.
/// </summary>
public static class PasswordResetRequested
{
    public sealed record V1(Guid UserId) : IIntegrationEvent;
}
