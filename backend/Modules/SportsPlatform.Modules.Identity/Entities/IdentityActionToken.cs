using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Identity.Entities;

public sealed class IdentityActionToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public IdentityActionTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    private IdentityActionToken() { }

    public IdentityActionToken(
        Guid id,
        Guid userId,
        IdentityActionTokenPurpose purpose,
        string tokenHash,
        DateTime expiresAt) : base(id)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("A token hash is required.", nameof(tokenHash));
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt.ToUniversalTime();
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsUsable(DateTime now) => ConsumedAt is null && ExpiresAt > now;

    public void Consume(DateTime now)
    {
        if (!IsUsable(now))
            throw new InvalidOperationException("The identity action token is no longer valid.");
        ConsumedAt = now.ToUniversalTime();
        UpdatedAt = ConsumedAt;
    }

    public void Revoke(DateTime now)
    {
        if (ConsumedAt is not null) return;
        ConsumedAt = now.ToUniversalTime();
        UpdatedAt = ConsumedAt;
    }
}

public enum IdentityActionTokenPurpose
{
    PasswordReset = 1,
    EmailVerification = 2
}
