using System.Buffers.Text;
using System.Globalization;
using System.Text;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary>
/// The opaque cursor of the venue directory: the sort key of a page's last venue (its sort key and ID),
/// base64url-encoded.
/// </summary>
public sealed record VenueCursor(string SortKey, Guid Id)
{
    private const char _separator = '\n';
    // Room for a sort key of 120 characters at three UTF-8 bytes each, the ID and base64's extra third.
    private const int _maxLength = 600;

    public string Encode() => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{SortKey}{_separator}{Id:N}")));

    public static bool TryDecode(string? text, out VenueCursor cursor)
    {
        cursor = new VenueCursor(string.Empty, Guid.Empty);
        if (text is null || text.Length is 0 or > _maxLength || !Base64Url.IsValid(text))
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
            || parts[0].Length is 0 or > VenueDetails.NameMaxLength
            || !Guid.TryParseExact(parts[1], "N", out var id))
        {
            return false;
        }

        var candidate = new VenueCursor(parts[0], id);
        if (candidate.Encode() != text)
        {
            return false;
        }

        cursor = candidate;
        return true;
    }
}
