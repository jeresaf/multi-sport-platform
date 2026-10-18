using System.Buffers.Binary;
using System.Buffers.Text;

namespace SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

/// <summary>
/// The opaque <c>cursor</c> of the audit list: the last entry of a page as <c>(occurred_at, id)</c>, the list's sort
/// key, base64url-encoded so clients don't build it themselves.
/// </summary>
public static class AuditCursor
{
    private const int _byteLength = 24;

    public static string Encode(DateTimeOffset occurredAt, Guid id)
    {
        Span<byte> bytes = stackalloc byte[_byteLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, occurredAt.UtcTicks);
        id.TryWriteBytes(bytes[8..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default;
        id = Guid.Empty;
        Span<byte> bytes = stackalloc byte[_byteLength];
        if (cursor is not { Length: 32 } || !Base64Url.IsValid(cursor)
            || !Base64Url.TryDecodeFromChars(cursor, bytes, out var written) || written != _byteLength)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        id = new Guid(bytes[8..], bigEndian: true);
        return true;
    }
}
