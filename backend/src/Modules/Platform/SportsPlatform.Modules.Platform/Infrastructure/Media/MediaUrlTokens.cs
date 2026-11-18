using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>
/// The local adapter's signed-URL tokens (S06 D17): Data Protection (purpose <see cref="Purpose"/>) encrypts and signs
/// the tenant, the asset, the variant and the expiry, so a token can't be read, changed or moved to another file. The
/// expiry is compared with <see cref="TimeProvider"/>, so tests can move the clock. Output is base64url: safe in a path.
/// </summary>
public sealed class MediaUrlTokens(IDataProtectionProvider dataProtection, TimeProvider timeProvider)
{
    public const string Purpose = "platform.media";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid tenantId, Guid assetId, MediaVariant variant)
    {
        var expiresAt = timeProvider.GetUtcNow() + Lifetime;
        var payload = new Payload(tenantId, assetId, variant, expiresAt.ToUnixTimeMilliseconds());
        return (_protector.Protect(JsonSerializer.Serialize(payload)), expiresAt);
    }

    /// <summary>What <paramref name="token"/> names, if it is this purpose's and unexpired; otherwise null.</summary>
    public MediaUrlToken? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(token));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }

        return payload is not null && timeProvider.GetUtcNow().ToUnixTimeMilliseconds() < payload.ExpiresAt
            ? new MediaUrlToken(payload.TenantId, payload.AssetId, payload.Variant)
            : null;
    }

    private sealed record Payload(Guid TenantId, Guid AssetId, MediaVariant Variant, long ExpiresAt);
}

/// <summary>The file a valid token names.</summary>
public sealed record MediaUrlToken(Guid TenantId, Guid AssetId, MediaVariant Variant);
