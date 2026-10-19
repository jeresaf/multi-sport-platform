using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;

/// <summary>
/// Identity's password reset token provider (S03 D13, D15): <c>{userId}.{secret}</c>, the secret 32 random bytes in
/// base64url. Only <c>{expiresAt}.{SHA-256 of the token}</c> is stored, under one name per user in
/// <c>identity.user_tokens</c>, so a newer token replaces the older one. A token lasts 1 hour; the reset deletes it, so it
/// works once. The user ID lets the reset find the user without an email in the link. Like the user store, it never
/// saves: the caller's save commits the stored hash.
/// </summary>
public sealed class PasswordResetTokenProvider(TimeProvider timeProvider) : IUserTwoFactorTokenProvider<User>
{
    public const string ProviderName = "MspPasswordReset";
    public const string LoginProvider = "msp";
    public const string TokenName = "password_reset";

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private const int _secretBytes = 32;

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);
        RequireResetPurpose(purpose);

        var token = $"{user.Id:N}.{WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(_secretBytes))}";
        var expiresAt = timeProvider.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        var stored = $"{expiresAt.ToString(CultureInfo.InvariantCulture)}.{Convert.ToBase64String(Hash(token))}";
        var result = await manager.SetAuthenticationTokenAsync(user, LoginProvider, TokenName, stored);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Storing the reset token failed: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
        }

        return token;
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);
        RequireResetPurpose(purpose);

        if (TryReadUserId(token) != user.Id
            || await manager.GetAuthenticationTokenAsync(user, LoginProvider, TokenName) is not { } stored
            || stored.Split('.') is not [var expiresPart, var hashPart]
            || !long.TryParse(expiresPart, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt)
            || timeProvider.GetUtcNow().ToUnixTimeSeconds() >= expiresAt)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        return Convert.TryFromBase64String(hashPart, expected, out var written)
            && written == SHA256.HashSizeInBytes
            && CryptographicOperations.FixedTimeEquals(expected, Hash(token));
    }

    /// <summary>The token is used: the caller's save deletes it, with the new password.</summary>
    public static Task<IdentityResult> ConsumeAsync(UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager.RemoveAuthenticationTokenAsync(user, LoginProvider, TokenName);
    }

    /// <summary>The user ID a token names, or null when it isn't shaped like one of these tokens.</summary>
    public static Guid? TryReadUserId(string? token) =>
        token?.Split('.') is [var id, { Length: > 0 }] && id.Length == 32 && Guid.TryParseExact(id, "N", out var userId)
            ? userId
            : null;

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user) => Task.FromResult(false);

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static void RequireResetPurpose(string purpose)
    {
        if (purpose != UserManager<User>.ResetPasswordTokenPurpose)
        {
            throw new InvalidOperationException($"{nameof(PasswordResetTokenProvider)} only makes password reset tokens, not '{purpose}'.");
        }
    }
}
