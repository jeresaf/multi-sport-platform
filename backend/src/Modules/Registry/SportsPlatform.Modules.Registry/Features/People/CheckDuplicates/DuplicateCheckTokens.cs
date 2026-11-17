using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// The duplicate check's token (S06 D6): proof that the actor ran the check for these details in this tenant less than
/// <see cref="Lifetime"/> ago, and which people it answered. Data Protection (purpose <see cref="Purpose"/>) encrypts
/// and signs it, so the token is opaque: it holds a hash of the normalised inputs, not the inputs. The expiry is kept
/// in the token and compared with <see cref="TimeProvider"/>, so tests can move the clock.
/// </summary>
public sealed class DuplicateCheckTokens(
    IDataProtectionProvider dataProtection, TimeProvider timeProvider, ITenantContext tenant, ICurrentActor actor)
{
    public const string Purpose = "registry.duplicate-check";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    /// <summary>A token for <paramref name="input"/> and the people the check answered.</summary>
    public string Issue(DuplicateCheckInput input, IEnumerable<Guid> candidateIds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(candidateIds);

        var payload = new Payload(
            TenantId(),
            actor.Kind,
            actor.ActorId,
            Hash(input),
            [.. candidateIds.Distinct().Order()],
            (timeProvider.GetUtcNow() + Lifetime).ToUnixTimeMilliseconds());
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// The people the check answered, when <paramref name="token"/> is this module's, unexpired, and was issued to the
    /// current actor in the scope's tenant for exactly <paramref name="input"/>; otherwise null.
    /// </summary>
    public IReadOnlySet<Guid>? Read(string? token, DuplicateCheckInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
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

        var valid = payload is not null
            && payload.TenantId == TenantId()
            && payload.ActorKind == actor.Kind
            && payload.ActorId == actor.ActorId
            && string.Equals(payload.InputHash, Hash(input), StringComparison.Ordinal)
            && timeProvider.GetUtcNow().ToUnixTimeMilliseconds() < payload.ExpiresAt;
        return valid ? payload!.CandidateIds.ToHashSet() : null;
    }

    /// <summary>
    /// SHA-256 of the normalised inputs (what the check compares, nothing else), so two requests that the check can't
    /// tell apart share a token, and any other difference doesn't.
    /// </summary>
    public static string Hash(DuplicateCheckInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string[] parts =
        [
            input.NameKey,
            input.LastNameKey,
            input.DateOfBirth?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            input.Nationality ?? string.Empty,
            input.SecondaryNationality ?? string.Empty,
            input.PersonId?.ToString("N") ?? string.Empty,
            .. input.Identifiers
                .Select(identifier => $"{identifier.Authority}\u001f{identifier.Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    }

    private Guid TenantId() =>
        tenant.TenantId ?? throw new InvalidOperationException("The duplicate check runs under the request's tenant.");

    private sealed record Payload(
        Guid TenantId, ActorKind ActorKind, Guid ActorId, string InputHash, Guid[] CandidateIds, long ExpiresAt);
}
