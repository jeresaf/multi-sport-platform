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
    private const char _separator = '\n';

    public string Encode() => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{SortKey}{_separator}{Id:N}")));

    /// <summary>
    /// Decodes a cursor this directory gave: a sort key of 1 to <paramref name="sortKeyMaxLength"/> characters and an ID,
    /// encoded exactly as <see cref="Encode"/> does.
    /// </summary>
    public static bool TryDecode(string? text, int sortKeyMaxLength, out DirectoryCursor cursor)
    {
        cursor = new DirectoryCursor(string.Empty, Guid.Empty);
        if (text is null || text.Length == 0 || text.Length > MaxEncodedLength(sortKeyMaxLength) || !Base64Url.IsValid(text))
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
        if (parts.Length != 2
            || parts[0].Length == 0
            || parts[0].Length > sortKeyMaxLength
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

    // A sort key at three UTF-8 bytes a character (a key of four-byte characters is shorter in chars than bytes / 3 * 4
    // would need, since a surrogate pair counts two), the separator and 32 hex digits, base64url without padding.
    private static int MaxEncodedLength(int sortKeyMaxLength) => (((sortKeyMaxLength * 3) + 1 + 32) * 4 + 2) / 3;
}
