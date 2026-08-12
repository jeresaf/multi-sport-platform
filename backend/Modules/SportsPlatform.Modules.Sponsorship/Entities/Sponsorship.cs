using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Sponsorship.Entities;

public class Sponsorship : TenantAggregateRoot<Guid>
{
    public Guid SponsorId { get; private set; }
    public Guid? ContestId { get; private set; }
    public Guid? CompetitionId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public SponsorshipType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Value { get; private set; }
    public string Currency { get; private set; } = "USD";
    public SponsorshipStatus Status { get; private set; } = SponsorshipStatus.Proposed;
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? Benefits { get; private set; }
    public string? ContractUrl { get; private set; }
    public string Placement { get; private set; } = SponsorPlacements.PublicHome;
    public int DisplayOrder { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Sponsorship() { }

    public Sponsorship(
        Guid id,
        Guid tenantId,
        Guid sponsorId,
        SponsorshipType type,
        string name,
        decimal value) : base(id, tenantId)
    {
        SponsorId = sponsorId;
        Type = type;
        Name = name;
        Value = value;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateTerms(SponsorshipType type, string name, decimal value)
    {
        Type = type;
        Name = name;
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAssociation(Guid? competitionId, Guid? contestId)
    {
        CompetitionId = competitionId;
        ContestId = contestId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContest(Guid contestId)
    {
        ContestId = contestId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCompetition(Guid competitionId)
    {
        CompetitionId = competitionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOrganization(Guid organizationId)
    {
        OrganizationId = organizationId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCurrency(string currency)
    {
        Currency = currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDates(DateTime? startDate, DateTime? endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetBenefits(string? benefits)
    {
        Benefits = benefits;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContractUrl(string? contractUrl)
    {
        ContractUrl = contractUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPlacement(string placement, int displayOrder)
    {
        Placement = placement;
        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = SponsorshipStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        Status = SponsorshipStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Terminate()
    {
        Status = SponsorshipStatus.Terminated;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class Sponsor : TenantAggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Website { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public SponsorTier Tier { get; private set; } = SponsorTier.Standard;
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Sponsor() { }

    public Sponsor(Guid id, Guid tenantId, string name) : base(id, tenantId)
    {
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLogoUrl(string? logoUrl)
    {
        LogoUrl = logoUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetWebsite(string? website)
    {
        Website = website;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContact(string? email, string? phone)
    {
        ContactEmail = email;
        ContactPhone = phone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTier(SponsorTier tier)
    {
        Tier = tier;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum SponsorshipType
{
    Title = 0,
    Presenting = 1,
    Gold = 2,
    Silver = 3,
    Bronze = 4,
    Media = 5,
    Technical = 6,
    InKind = 7
}

public enum SponsorshipStatus
{
    Proposed = 0,
    Negotiating = 1,
    Active = 2,
    Expired = 3,
    Terminated = 4,
    Rejected = 5
}

public enum SponsorTier
{
    Standard = 0,
    Silver = 1,
    Gold = 2,
    Platinum = 3,
    Diamond = 4
}

public static class SponsorPlacements
{
    public const string PublicHome = "PublicHome";
    public const string CompetitionHeader = "CompetitionHeader";
    public const string ContestHeader = "ContestHeader";
    public const string ContestSheet = "ContestSheet";
    public const string Sidebar = "Sidebar";
    public const string Footer = "Footer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PublicHome,
        CompetitionHeader,
        ContestHeader,
        ContestSheet,
        Sidebar,
        Footer
    };
}
