using System.Globalization;
using System.Text;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// Normalised names for search and the duplicate check (S06 D4). A name part is trimmed, lower-cased (invariant), its
/// accents removed (NFD, non-spacing marks dropped), its apostrophes, hyphens and dots removed and its inner whitespace
/// collapsed to one space. The <b>name key</b> is the normalised tokens of every part, sorted and joined with one space,
/// so "Ssekandi John" and "John Ssekandi" share <c>john ssekandi</c>.
/// </summary>
public static class NameNormaliser
{
    // Apostrophes (straight, curly, modifier letter, backtick), hyphens and dashes, and dots: "Ng'ang'a" → "nganga",
    // "Okello-Owino" → "okelloowino", "J." → "j".
    private const string _removed = "'’‘ʼ`´-‐‑‒–—.";

    /// <summary>One name part, normalised; empty for null or blank.</summary>
    public static string Normalise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark
                || _removed.Contains(character, StringComparison.Ordinal))
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>The name key of a set of name parts, whatever their order; empty when every part is blank.</summary>
    public static string NameKey(params ReadOnlySpan<string?> parts)
    {
        var tokens = new List<string>();
        foreach (var part in parts)
        {
            tokens.AddRange(Normalise(part).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        tokens.Sort(StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }
}
