using System.Buffers.Text;

namespace SportsPlatform.Modules.Identity.Features;

/// <summary>The opaque cursor of a list sorted by UUIDv7 ID: the last ID of a page, base64url-encoded.</summary>
internal static class IdCursor
{
    public static string Encode(Guid id) => Base64Url.EncodeToString(id.ToByteArray(bigEndian: true));

    public static bool TryDecode(string? cursor, out Guid id)
    {
        id = Guid.Empty;
        if (cursor is null || cursor.Length != 22 || !Base64Url.IsValid(cursor))
        {
            return false;
        }

        var decoded = new Guid(Base64Url.DecodeFromChars(cursor), bigEndian: true);
        if (Encode(decoded) != cursor)
        {
            return false;
        }

        id = decoded;
        return true;
    }
}
