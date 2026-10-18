using System.Buffers.Text;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// The opaque <c>cursor</c> of the outbox diagnostics list: the last message ID of a page, base64url-encoded so
/// clients don't treat it as an ID.
/// </summary>
public static class OutboxCursor
{
    public static string Encode(Guid lastId)
    {
        Span<byte> bytes = stackalloc byte[16];
        lastId.TryWriteBytes(bytes, bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string? cursor, out Guid lastId)
    {
        lastId = Guid.Empty;
        Span<byte> bytes = stackalloc byte[16];
        if (cursor is not { Length: 22 } || !Base64Url.IsValid(cursor)
            || !Base64Url.TryDecodeFromChars(cursor, bytes, out var written) || written != 16)
        {
            return false;
        }

        // 22 characters carry 132 bits: reject the non-canonical spellings of the same ID.
        var decoded = new Guid(bytes, bigEndian: true);
        if (Encode(decoded) != cursor)
        {
            return false;
        }

        lastId = decoded;
        return true;
    }
}
