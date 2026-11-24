using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace SportsPlatform.Modules.Registry.Features;

/// <summary>
/// The opaque cursor of a registry directory sorted by sort key and ID (venues, organisations): the sort key and ID of
/// a page's last record, base64url-encoded.
/// </summary>
public sealed record DirectoryCursor(string SortKey, Guid Id)
{
    public string Encode() => CursorText.Encode(SortKey, Id.ToString("N", CultureInfo.InvariantCulture));

    /// <summary>
    /// Decodes a cursor this directory gave: a sort key of 1 to <paramref name="sortKeyMaxLength"/> characters and an ID,
    /// encoded exactly as <see cref="Encode"/> does.
    /// </summary>
    public static bool TryDecode(string? text, int sortKeyMaxLength, out DirectoryCursor cursor)
    {
        cursor = new DirectoryCursor(string.Empty, Guid.Empty);
        if (!CursorText.TryDecode(text, sortKeyMaxLength, 2, out var parts)
            || !Guid.TryParseExact(parts[1], "N", out var id))
        {
            return false;
        }

        var candidate = new DirectoryCursor(parts[0], id);
        if (candidate.Encode() != text)
        {
            return false;
        }

        cursor = candidate;
        return true;
    }
}

/// <summary>
/// The text of a registry directory's cursor: a sort key first, then the other fields (an ID last), separated by line
/// feeds and base64url-encoded. The cursors check their own fields and that they re-encode to the same text.
/// </summary>
internal static class CursorText
{
    private const char _separator = '\n';

    // Room for an ID (32 hex digits) and a short number per field after the sort key.
    private const int _otherFieldMaxBytes = 33;

    public static string Encode(params string[] fields) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Join(_separator, fields)));

    /// <summary>
    /// The <paramref name="fieldCount"/> fields of <paramref name="text"/>, the first a sort key of 1 to
    /// <paramref name="sortKeyMaxLength"/> characters; false for text that isn't such a cursor.
    /// </summary>
    public static bool TryDecode(string? text, int sortKeyMaxLength, int fieldCount, out string[] fields)
    {
        fields = [];
        if (text is null || text.Length == 0 || text.Length > MaxEncodedLength(sortKeyMaxLength, fieldCount) || !Base64Url.IsValid(text))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(Base64Url.DecodeFromChars(text));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return false;
        }

        var parts = decoded.Split(_separator);
        if (parts.Length != fieldCount || parts[0].Length == 0 || parts[0].Length > sortKeyMaxLength)
        {
            return false;
        }

        fields = parts;
        return true;
    }

    // A sort key at three UTF-8 bytes a character (a key of four-byte characters is shorter in chars than bytes / 3 * 4
    // would need, since a surrogate pair counts two), a separator and a field for each other field, base64url without
    // padding.
    private static int MaxEncodedLength(int sortKeyMaxLength, int fieldCount) =>
        (((sortKeyMaxLength * 3) + ((fieldCount - 1) * _otherFieldMaxBytes)) * 4 + 2) / 3;
}
