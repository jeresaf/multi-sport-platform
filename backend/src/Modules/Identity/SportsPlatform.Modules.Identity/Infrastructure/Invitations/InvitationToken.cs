using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace SportsPlatform.Modules.Identity.Infrastructure.Invitations;

/// <summary>
/// An invitation token (S03 D12): 32 random bytes in base64url, sent once in the email link. Only its SHA-256 is stored;
/// it is unique across tenants, so the hash alone finds the invitation.
/// </summary>
public static class InvitationToken
{
    private const int _secretBytes = 32;

    public static (string Token, byte[] Hash) Create()
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(_secretBytes));
        return (token, Hash(token));
    }

    public static byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
