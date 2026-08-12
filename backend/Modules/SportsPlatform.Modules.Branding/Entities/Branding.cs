using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Branding.Entities;

public class Brand : AggregateRoot<Guid>
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Tagline { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? FaviconUrl { get; private set; }
    public string? PrimaryColor { get; private set; } = "#000000";
    public string? SecondaryColor { get; private set; } = "#FFFFFF";
    public string? AccentColor { get; private set; } = "#0000FF";
    public string? FontFamily { get; private set; }
    public string? CustomCss { get; private set; }
    public string? EmailTemplate { get; private set; }
    public bool IsDefault { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Brand() { }

    public Brand(Guid id, Guid organizationId, string name) : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetTagline(string? tagline)
    {
        Tagline = tagline;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLogo(string? logoUrl, string? faviconUrl)
    {
        LogoUrl = logoUrl;
        FaviconUrl = faviconUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetColors(string? primary, string? secondary, string? accent)
    {
        PrimaryColor = primary ?? PrimaryColor;
        SecondaryColor = secondary ?? SecondaryColor;
        AccentColor = accent ?? AccentColor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetFontFamily(string? fontFamily)
    {
        FontFamily = fontFamily;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCustomCss(string? customCss)
    {
        CustomCss = customCss;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEmailTemplate(string? emailTemplate)
    {
        EmailTemplate = emailTemplate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class BrandAsset : Entity<Guid>
{
    public Guid BrandId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AssetType Type { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? ThumbnailUrl { get; private set; }
    public string? Metadata { get; private set; }
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }

    private BrandAsset() { }

    public BrandAsset(Guid id, Guid brandId, string name, AssetType type, string url) : base(id)
    {
        BrandId = brandId;
        Name = name;
        Type = type;
        Url = url;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetThumbnail(string? thumbnailUrl)
    {
        ThumbnailUrl = thumbnailUrl;
    }

    public void SetMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}

public enum AssetType
{
    Logo = 0,
    Icon = 1,
    Banner = 2,
    Background = 3,
    SponsorLogo = 4,
    Photo = 5,
    Video = 6,
    Document = 7
}
