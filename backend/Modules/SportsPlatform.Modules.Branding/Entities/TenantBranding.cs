using System.Net.Mail;
using System.Text.RegularExpressions;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Branding.Entities;

public class TenantBranding : TenantAggregateRoot<Guid>
{
    public string TenantName { get; private set; } = string.Empty;
    public string? ShortName { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? AccentColor { get; private set; }
    public string? PublicSlug { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Website { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private TenantBranding() { }

    public TenantBranding(Guid id, Guid tenantId, string tenantName) : base(id, tenantId)
    {
        TenantName = Required(tenantName, nameof(tenantName), 200);
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateBranding(
        string tenantName,
        string? shortName,
        string? logoUrl,
        string? primaryColor,
        string? secondaryColor,
        string? accentColor)
    {
        TenantName = Required(tenantName, nameof(tenantName), 200);
        ShortName = Optional(shortName, nameof(shortName), 100);
        LogoUrl = WebUrl(logoUrl, nameof(logoUrl));
        PrimaryColor = Color(primaryColor, nameof(primaryColor));
        SecondaryColor = Color(secondaryColor, nameof(secondaryColor));
        AccentColor = Color(accentColor, nameof(accentColor));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePublicInfo(string? publicSlug, string? contactEmail, string? contactPhone, string? website)
    {
        PublicSlug = Slug(publicSlug);
        ContactEmail = Email(contactEmail);
        ContactPhone = Optional(contactPhone, nameof(contactPhone), 50);
        Website = WebUrl(website, nameof(website));
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Required(string value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException("A value is required.", parameter);
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameter);
        return normalized;
    }

    private static string? Optional(string? value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameter);
        return normalized;
    }

    private static string? Color(string? value, string parameter)
    {
        var normalized = Optional(value, parameter, 7);
        if (normalized is null) return null;
        if (!Regex.IsMatch(normalized, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Brand colors must use the #RRGGBB hexadecimal format.", parameter);
        return normalized.ToUpperInvariant();
    }

    private static string? WebUrl(string? value, string parameter)
    {
        var normalized = Optional(value, parameter, 500);
        if (normalized is null) return null;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Assets and websites must use an absolute HTTP or HTTPS URL without embedded credentials.", parameter);
        return uri.AbsoluteUri;
    }

    private static string? Email(string? value)
    {
        var normalized = Optional(value, nameof(value), 200)?.ToLowerInvariant();
        if (normalized is not null && !MailAddress.TryCreate(normalized, out _))
            throw new ArgumentException("A valid contact email is required.", nameof(value));
        return normalized;
    }

    private static string? Slug(string? value)
    {
        var normalized = Optional(value, nameof(value), 100)?.ToLowerInvariant();
        if (normalized is not null && !Regex.IsMatch(
                normalized, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The public slug may contain only lowercase letters, numbers, and single hyphens.", nameof(value));
        return normalized;
    }
}
