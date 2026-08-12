using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Officials.Entities;

public class Official : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public string? DisplayName { get; private set; }
    public OfficialType Type { get; private set; }
    public string? LicenseNumber { get; private set; }
    public DateTime? LicenseExpiry { get; private set; }
    public OfficialStatus Status { get; private set; } = OfficialStatus.Active;
    public string? Specializations { get; private set; }
    public string? Certifications { get; private set; }
    public int YearsOfExperience { get; private set; }
    public string? Bio { get; private set; }
    public string? PhotoUrl { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Official() { }

    public Official(Guid id, Guid userId, string firstName, string lastName, OfficialType type) : base(id)
    {
        UserId = userId;
        FirstName = firstName;
        LastName = lastName;
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetMiddleName(string? middleName)
    {
        MiddleName = middleName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDisplayName(string? displayName)
    {
        DisplayName = displayName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLicense(string? licenseNumber, DateTime? expiry)
    {
        LicenseNumber = licenseNumber;
        LicenseExpiry = expiry;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSpecializations(string? specializations)
    {
        Specializations = specializations;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCertifications(string? certifications)
    {
        Certifications = certifications;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetExperience(int years)
    {
        YearsOfExperience = years;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetBio(string? bio)
    {
        Bio = bio;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPhotoUrl(string? photoUrl)
    {
        PhotoUrl = photoUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = OfficialStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = OfficialStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Retire()
    {
        Status = OfficialStatus.Retired;
        UpdatedAt = DateTime.UtcNow;
    }

    public string FullName => string.IsNullOrEmpty(MiddleName) 
        ? $"{FirstName} {LastName}" 
        : $"{FirstName} {MiddleName} {LastName}";
}

public class OfficialAssignment : Entity<Guid>
{
    public Guid OfficialId { get; private set; }
    public Guid ContestId { get; private set; }
    public Guid? EventId { get; private set; }
    public OfficialRole Role { get; private set; }
    public AssignmentStatus Status { get; private set; } = AssignmentStatus.Pending;
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? DeclinedAt { get; private set; }
    public string? DeclineReason { get; private set; }
    public new DateTime CreatedAt { get; private set; }

    private OfficialAssignment() { }

    public OfficialAssignment(Guid id, Guid officialId, Guid contestId, OfficialRole role) : base(id)
    {
        OfficialId = officialId;
        ContestId = contestId;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetEventId(Guid? eventId)
    {
        EventId = eventId;
    }

    public void Confirm()
    {
        Status = AssignmentStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Decline(string? reason)
    {
        Status = AssignmentStatus.Declined;
        DeclinedAt = DateTime.UtcNow;
        DeclineReason = reason;
    }

    public void Cancel()
    {
        Status = AssignmentStatus.Cancelled;
    }
}

public enum OfficialType
{
    Referee = 0,
    Umpire = 1,
    Judge = 2,
    Linesman = 3,
    Scorer = 4,
    Timekeeper = 5,
    TechnicalOfficial = 6
}

public enum OfficialRole
{
    HeadOfficial = 0,
    AssistantOfficial = 1,
    TechnicalController = 2,
    JuryMember = 3
}

public enum OfficialStatus
{
    Active = 0,
    Suspended = 1,
    Retired = 2,
    Inactive = 3
}

public enum AssignmentStatus
{
    Pending = 0,
    Confirmed = 1,
    Declined = 2,
    Cancelled = 3
}
