using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Identity.Services;

namespace SportsPlatform.Api.Services;

public sealed class TotpMfaService : IMfaService
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private readonly IDataProtector _protector;

    public TotpMfaService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("SportsPlatform.Identity.MfaSecret.v1");

    public string BeginEnrollment(User user)
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        var secret = Base32Encode(bytes);
        user.BeginMfaEnrollment(_protector.Protect(secret));
        return secret;
    }

    public bool VerifyCode(User user, string? code)
    {
        if (string.IsNullOrWhiteSpace(user.MfaSecretProtected)
            || code is null || code.Length != 6 || !code.All(char.IsAsciiDigit))
            return false;

        string secret;
        try { secret = _protector.Unprotect(user.MfaSecretProtected); }
        catch (CryptographicException) { return false; }

        var key = Base32Decode(secret);
        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(GenerateCode(key, currentStep + offset)),
                System.Text.Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    private static string GenerateCode(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static string Base32Encode(byte[] bytes)
    {
        var output = new System.Text.StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in value.ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(character);
            if (index < 0) continue;
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }
}
