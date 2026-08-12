using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Contests.Entities;

public class ContestEntry : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string ParticipantType { get; private set; } = string.Empty;
    public string? EntryNumber { get; private set; }
    public string? Seed { get; private set; }
    public ContestEntryStatus Status { get; private set; } = ContestEntryStatus.Pending;
    public DateTime? RegisteredAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private ContestEntry() { }

    public ContestEntry(Guid id, Guid contestId, Guid tenantId, Guid participantId, string participantType)
        : base(id, tenantId)
    {
        ContestId = contestId;
        ParticipantId = participantId;
        ParticipantType = participantType;
        RegisteredAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void AssignEntryNumber(string entryNumber)
    {
        EntryNumber = entryNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignSeed(string seed)
    {
        Seed = seed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = ContestEntryStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        Status = ContestEntryStatus.Rejected;
        RejectedAt = DateTime.UtcNow;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Withdraw()
    {
        Status = ContestEntryStatus.Withdrawn;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        Status = ContestEntryStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum ContestEntryStatus
{
    Pending,
    Approved,
    Confirmed,
    Rejected,
    Withdrawn,
    Waitlisted
}
