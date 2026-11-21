using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Registry.Domain;

/// <summary>
/// The slugs of venues, organisations and teams (S07 D9): lower-case ASCII letters and digits in words joined by single
/// hyphens, 2–80 characters, unique per tenant and entity type among every status. A slug never parses as a UUID, so
/// <c>{idOrSlug}</c> stays unambiguous. A proposal comes from the name (and, for teams, the category); the use case adds
/// <c>-2</c>, <c>-3</c>… on a clash (<see cref="WithSuffix"/>).
/// </summary>
public static partial class Slugs
{
    public const int MinLength = 2;
    public const int MaxLength = 80;

    // Room for "-" and a number up to six digits.
    private const int _proposalMaxLength = MaxLength - 7;

    /// <summary>Whether <paramref name="slug"/> is well formed and not a UUID.</summary>
    public static bool IsValid(string? slug) =>
        slug is { Length: >= MinLength and <= MaxLength } && Pattern().IsMatch(slug) && !Guid.TryParse(slug, out _);

    /// <summary>
    /// A slug from <paramref name="parts"/>: accents removed, letters and digits outside ASCII and everything else
    /// turned into hyphens, cut to leave room for a suffix. <paramref name="fallback"/> (itself a valid slug) when
    /// nothing is left or the result would be a UUID.
    /// </summary>
    public static string Propose(string fallback, params ReadOnlySpan<string?> parts)
    {
        if (!IsValid(fallback))
        {
            throw new ArgumentException("The fallback must be a valid slug.", nameof(fallback));
        }

        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            foreach (var character in (part ?? string.Empty).Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                var lower = char.ToLowerInvariant(character);
                builder.Append(char.IsAsciiLetterLower(lower) || char.IsAsciiDigit(lower) ? lower : '-');
            }

            builder.Append('-');
        }

        var slug = Hyphens().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length > _proposalMaxLength)
        {
            slug = slug[.._proposalMaxLength].TrimEnd('-');
        }

        return IsValid(slug) ? slug : fallback;
    }

    /// <summary><paramref name="slug"/> with <c>-<paramref name="number"/></c>; 1 is the slug itself.</summary>
    public static string WithSuffix(string slug, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return number == 1 ? slug : $"{slug}-{number.ToString(CultureInfo.InvariantCulture)}";
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex("-{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Hyphens();
}
