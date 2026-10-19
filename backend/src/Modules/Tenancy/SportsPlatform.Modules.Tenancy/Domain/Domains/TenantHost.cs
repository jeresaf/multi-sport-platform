using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Tenancy.Domain.Domains;

/// <summary>Host names as stored in <c>tenant_domains</c> and as looked up: lower case, no port, no trailing dot.</summary>
public static partial class TenantHost
{
    public const int MaxLength = 253;

    /// <summary>The host of <paramref name="value"/> (a <c>Host</c> header value, port allowed), or null if it isn't one.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var host = value.Trim();
        var colon = host.LastIndexOf(':');
        if (colon >= 0 && host.AsSpan(colon + 1).IndexOfAnyExceptInRange('0', '9') < 0)
        {
            host = host[..colon];
        }

        host = host.TrimEnd('.').ToLowerInvariant();
        return IsValid(host) ? host : null;
    }

    /// <summary>Whether <paramref name="host"/> is already a normalized DNS name (letters, digits, hyphens, dots).</summary>
    public static bool IsValid(string? host) => host is { Length: > 0 and <= MaxLength } && HostPattern().IsMatch(host);

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();
}
