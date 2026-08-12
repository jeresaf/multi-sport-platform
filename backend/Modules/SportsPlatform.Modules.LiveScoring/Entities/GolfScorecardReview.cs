using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.LiveScoring.Entities;

public sealed class GolfScorecardReview : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int RoundNumber { get; private set; }
    public string HolesJson { get; private set; } = "[]";
    public GolfScorecardReviewStatus Status { get; private set; }
    public Guid SubmittedBy { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }

    private GolfScorecardReview() { }

    public GolfScorecardReview(
        Guid id,
        Guid tenantId,
        Guid contestId,
        Guid participantId,
        int roundNumber,
        string holesJson,
        Guid submittedBy) : base(id, tenantId)
    {
        ContestId = contestId;
        ParticipantId = participantId;
        RoundNumber = roundNumber;
        HolesJson = holesJson;
        SubmittedBy = submittedBy;
        SubmittedAt = DateTime.UtcNow;
        Status = GolfScorecardReviewStatus.Pending;
    }

    public void Approve(Guid reviewedBy, string? notes)
    {
        EnsurePending();
        Status = GolfScorecardReviewStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes?.Trim();
    }

    public void Reject(Guid reviewedBy, string notes)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Rejection notes are required.", nameof(notes));
        Status = GolfScorecardReviewStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes.Trim();
    }

    private void EnsurePending()
    {
        if (Status != GolfScorecardReviewStatus.Pending)
            throw new InvalidOperationException("Only pending scorecards can be reviewed.");
    }
}

public enum GolfScorecardReviewStatus
{
    Pending,
    Approved,
    Rejected
}
