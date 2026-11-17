using System.Buffers.Text;
using System.Globalization;
using System.Text;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// The opaque cursor of the people directory: the sort key of a page's last person (normalised last name, normalised
/// first name, ID), base64url-encoded.
/// </summary>
public sealed record PersonCursor(string LastNameKey, string FirstNameKey, Guid Id)
{
    private const char _separator = '\n';
    // Room for two name keys of 100 characters at three UTF-8 bytes each, the ID and base64's extra third.
    private const int _maxLength = 1000;

    public string Encode() => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{LastNameKey}{_separator}{FirstNameKey}{_separator}{Id:N}")));

    public static bool TryDecode(string? text, out PersonCursor cursor)
    {
        cursor = new PersonCursor(string.Empty, string.Empty, Guid.Empty);
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
        if (parts.Length != 3
            || parts[0].Length is 0 or > PersonBiodata.NameMaxLength
            || parts[1].Length is 0 or > PersonBiodata.NameMaxLength
            || !Guid.TryParseExact(parts[2], "N", out var id))
        {
            return false;
        }

        var candidate = new PersonCursor(parts[0], parts[1], id);
        if (candidate.Encode() != text)
        {
            return false;
        }

        cursor = candidate;
        return true;
    }
}
