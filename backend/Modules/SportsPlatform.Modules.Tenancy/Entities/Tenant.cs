using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Tenancy.Entities;

public class Tenant : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Country { get; private set; }
    public string? Timezone { get; private set; }
    public string? Currency { get; private set; }
    public string? DefaultLanguage { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }

    private Tenant() { }

    public Tenant(Guid id, string name, string shortName, string slug) : base(id)
    {
        Name = Required(name, nameof(name), 200);
        ShortName = Required(shortName, nameof(shortName), 100);
        Slug = NormalizeSlug(slug);
        Status = TenantStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string shortName, string? description)
    {
        Name = Required(name, nameof(name), 200);
        ShortName = Required(shortName, nameof(shortName), 100);
        Description = Optional(description, nameof(description), 1000);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateContact(string? email, string? phone)
    {
        ContactEmail = Optional(email, nameof(email), 200);
        ContactPhone = Optional(phone, nameof(phone), 50);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLocalization(string? country, string? timezone, string? currency, string? defaultLanguage)
    {
        Country = Optional(country, nameof(country), 100);
        Timezone = Optional(timezone, nameof(timezone), 100);
        Currency = Optional(currency, nameof(currency), 10)?.ToUpperInvariant();
        DefaultLanguage = Optional(defaultLanguage, nameof(defaultLanguage), 10);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Archived)
            throw new InvalidOperationException("An archived tenant cannot be suspended.");
        if (Status == TenantStatus.Suspended) return;
        Status = TenantStatus.Suspended;
        SuspendedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status == TenantStatus.Archived)
            throw new InvalidOperationException("An archived tenant cannot be reactivated.");
        if (Status == TenantStatus.Active) return;
        Status = TenantStatus.Active;
        SuspendedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == TenantStatus.Archived) return;
        Status = TenantStatus.Archived;
        SuspendedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Required(string value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("A value is required.", parameter);
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

    private static string NormalizeSlug(string value)
    {
        var slug = Required(value, nameof(value), 100).ToLowerInvariant();
        if (slug.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new ArgumentException("The slug may contain only lowercase letters, numbers, and hyphens.", nameof(value));
        if (slug.StartsWith('-') || slug.EndsWith('-') || slug.Contains("--", StringComparison.Ordinal))
            throw new ArgumentException("The slug cannot start or end with a hyphen or contain consecutive hyphens.", nameof(value));
        return slug;
    }
}

public enum TenantStatus
{
    Active,
    Suspended,
    Archived
}
