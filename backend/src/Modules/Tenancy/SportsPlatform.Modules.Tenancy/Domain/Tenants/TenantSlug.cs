using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

/// <summary>
/// A tenant's slug: 3–40 lower-case letters, digits and hyphens, no hyphen at either end, not a reserved word. It names
/// the platform subdomain (<c>kmsa.&lt;platform domain&gt;</c>), never changes and is never reused, even after the tenant
/// is archived.
/// </summary>
public static partial class TenantSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    /// <summary>The format as the database check constraint has it (PostgreSQL's <c>$</c> is the end of the text).</summary>
    public const string Pattern = "^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$";

    // .NET's $ also matches before a final newline; \z doesn't.
    private const string _dotNetPattern = "^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])\\z";

    /// <summary>Subdomains the platform keeps for itself.</summary>
    public static IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "admin", "app", "platform", "static", "assets", "mail", "status", "docs",
    };

    public static bool HasValidFormat(string? slug) => slug is not null && Format().IsMatch(slug);

    public static bool IsReserved(string slug) => Reserved.Contains(slug);

    [GeneratedRegex(_dotNetPattern, RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
