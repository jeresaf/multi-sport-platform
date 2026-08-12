using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Results.Entities;

public class ContestResult : AggregateRoot<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public ContestResultStatus Status { get; private set; } = ContestResultStatus.Pending;
    public int? Rank { get; private set; }
    public string? Position { get; private set; }
    public decimal? Score { get; private set; }
    public string? ScoreDisplay { get; private set; }
    public string? Time { get; private set; }
    public string? PerformanceData { get; private set; }
    public string? Statistics { get; private set; }
    public bool IsPersonalBest { get; private set; }
    public bool IsSeasonBest { get; private set; }
    public bool IsCompetitionRecord { get; private set; }
    public string? Remarks { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private ContestResult() { }

    public ContestResult(Guid id, Guid contestId, Guid eventId, Guid participantId) : base(id)
    {
        ContestId = contestId;
        EventId = eventId;
        ParticipantId = participantId;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetScore(decimal? score, string? scoreDisplay, string? time)
    {
        Score = score;
        ScoreDisplay = scoreDisplay;
        Time = time;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRank(int? rank, string? position)
    {
        Rank = rank;
        Position = position;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPerformanceData(string? performanceData, string? statistics)
    {
        PerformanceData = performanceData;
        Statistics = statistics;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPersonalBest()
    {
        IsPersonalBest = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSeasonBest()
    {
        IsSeasonBest = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompetitionRecord()
    {
        IsCompetitionRecord = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRemarks(string? remarks)
    {
        Remarks = remarks;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid approvedBy)
    {
        if (Status == ContestResultStatus.Submitted) BeginReview();
        if (Status != ContestResultStatus.UnderReview)
            throw new InvalidOperationException("Only results under review can be approved.");
        Status = ContestResultStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string? reason)
    {
        if (Status == ContestResultStatus.Submitted) BeginReview();
        if (Status != ContestResultStatus.UnderReview)
            throw new InvalidOperationException("Only results under review can be rejected.");
        Status = ContestResultStatus.Rejected;
        Remarks = reason ?? Remarks;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status == ContestResultStatus.Submitted) return;
        if (Status is ContestResultStatus.Approved
            or ContestResultStatus.Published
            or ContestResultStatus.Official
            or ContestResultStatus.UnderReview)
        {
            throw new InvalidOperationException(
                "Results under review or already approved/published/official cannot be resubmitted.");
        }
        Status = ContestResultStatus.Submitted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void BeginReview()
    {
        if (Status == ContestResultStatus.UnderReview) return;
        if (Status != ContestResultStatus.Submitted)
            throw new InvalidOperationException("Only submitted results can enter review.");
        Status = ContestResultStatus.UnderReview;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReopenForCorrection(string reason)
    {
        if (Status is not (ContestResultStatus.Approved
            or ContestResultStatus.Published
            or ContestResultStatus.Official))
        {
            throw new InvalidOperationException(
                "Only approved, published, or official results can be reopened.");
        }
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reopening reason is required.", nameof(reason));
        Status = ContestResultStatus.Corrected;
        Remarks = reason.Trim();
        ApprovedBy = null;
        ApprovedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (Status != ContestResultStatus.Approved)
            throw new InvalidOperationException("Only approved results can be published.");
        Status = ContestResultStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum ContestResultStatus
{
    Pending = 0,
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Published = 5,
    Official = 6,
    UnderReview = 7,
    Corrected = 8
}
