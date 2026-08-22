using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Registrations.Entities;

public class Registration : AggregateRoot<Guid>
{
    public Guid? ContestId { get; private set; }
    public Guid? CompetitionId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid? ApplicantUserId { get; private set; }
    public Guid? FormSubmissionId { get; private set; }
    public RegistrationType Type { get; private set; }
    public RegistrationStatus Status { get; private set; } = RegistrationStatus.Draft;
    public DateTime? RegisteredAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? Notes { get; private set; }
    public string? EntryData { get; private set; }
    public decimal? EntryFee { get; private set; }
    public bool IsEntryFeePaid { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public string EligibilityIssuesJson { get; private set; } = "[]";
    public DateTime? EligibilityCheckedAt { get; private set; }
    public Guid? EligibilityOverriddenBy { get; private set; }
    public DateTime? EligibilityOverriddenAt { get; private set; }
    public string? EligibilityOverrideReason { get; private set; }

    private Registration() { }

    public Registration(Guid id, Guid contestId, Guid participantId, RegistrationType type) : base(id)
    {
        ContestId = contestId;
        ParticipantId = participantId;
        Type = type;
        Status = RegistrationStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public static Registration CreateForCompetition(
        Guid id,
        Guid competitionId,
        Guid participantId,
        RegistrationType type)
    {
        return new Registration
        {
            Id = id,
            CompetitionId = competitionId,
            ParticipantId = participantId,
            Type = type,
            Status = RegistrationStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Workflow methods
    public void Submit(Guid? submittedBy = null)
    {
        if (Status != RegistrationStatus.Draft)
            throw new InvalidOperationException("Only draft registrations can be submitted.");
        if (submittedBy == Guid.Empty)
            throw new ArgumentException("A valid submitter is required.", nameof(submittedBy));
        
        Status = RegistrationStatus.Submitted;
        SubmittedBy = submittedBy;
        RegisteredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartReview()
    {
        if (Status != RegistrationStatus.Submitted)
            throw new InvalidOperationException("Only submitted registrations can be reviewed.");
        
        Status = RegistrationStatus.UnderReview;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewerId, string? notes = null)
    {
        if (Status != RegistrationStatus.Submitted && Status != RegistrationStatus.UnderReview)
            throw new InvalidOperationException("Only submitted or under-review registrations can be approved.");
        
        ReviewNotes = RequiredReason(notes, "An approval reason is required.");
        Status = RegistrationStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordEligibility(IEnumerable<string> issues, Guid? overriddenBy = null, string? overrideReason = null)
    {
        var normalized = issues.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToArray();
        EligibilityIssuesJson = System.Text.Json.JsonSerializer.Serialize(normalized);
        EligibilityCheckedAt = DateTime.UtcNow;
        if (normalized.Length > 0 && overriddenBy.HasValue)
        {
            EligibilityOverrideReason = RequiredReason(overrideReason, "An eligibility override reason is required.");
            EligibilityOverriddenBy = overriddenBy;
            EligibilityOverriddenAt = DateTime.UtcNow;
        }
        else
        {
            EligibilityOverrideReason = null;
            EligibilityOverriddenBy = null;
            EligibilityOverriddenAt = null;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<string> EligibilityIssues() =>
        System.Text.Json.JsonSerializer.Deserialize<string[]>(EligibilityIssuesJson) ?? [];

    public void Reject(Guid reviewerId, string? reason = null)
    {
        if (Status != RegistrationStatus.Submitted && Status != RegistrationStatus.UnderReview)
            throw new InvalidOperationException("Only submitted or under-review registrations can be rejected.");
        
        ReviewNotes = RequiredReason(reason, "A rejection reason is required.");
        Status = RegistrationStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddToWaitlist(Guid reviewerId, string? notes = null)
    {
        if (Status != RegistrationStatus.Submitted && Status != RegistrationStatus.UnderReview)
            throw new InvalidOperationException("Only submitted or under-review registrations can be waitlisted.");
        
        ReviewNotes = RequiredReason(notes, "A waitlist reason is required.");
        Status = RegistrationStatus.Waitlisted;
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != RegistrationStatus.Approved && Status != RegistrationStatus.Waitlisted)
            throw new InvalidOperationException("Only approved or waitlisted registrations can be confirmed.");
        
        Status = RegistrationStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Withdraw(string? reason = null)
    {
        if (Status == RegistrationStatus.Confirmed || Status == RegistrationStatus.Cancelled)
            throw new InvalidOperationException("Cannot withdraw a confirmed or already cancelled registration.");
        
        CancellationReason = RequiredReason(reason, "A withdrawal reason is required.");
        Status = RegistrationStatus.Withdrawn;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status == RegistrationStatus.Cancelled)
            throw new InvalidOperationException("Registration is already cancelled.");
        
        Status = RegistrationStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsNoShow()
    {
        if (Status != RegistrationStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed registrations can be marked as no-show.");
        
        Status = RegistrationStatus.NoShow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEntryData(string? entryData)
    {
        EntryData = entryData;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEntryFee(decimal? fee, bool isPaid)
    {
        EntryFee = fee;
        IsEntryFeePaid = isPaid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignApplicant(Guid applicantUserId)
    {
        if (applicantUserId == Guid.Empty)
            throw new ArgumentException("An applicant user is required.", nameof(applicantUserId));
        ApplicantUserId = applicantUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkFormSubmission(Guid formSubmissionId)
    {
        if (formSubmissionId == Guid.Empty)
            throw new ArgumentException("A form submission is required.", nameof(formSubmissionId));
        FormSubmissionId = formSubmissionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEntryFeeAsPaid()
    {
        IsEntryFeePaid = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string RequiredReason(string? reason, string message)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(message, nameof(reason));
        var normalized = reason.Trim();
        if (normalized.Length > 1000)
            throw new ArgumentException("A registration reason cannot exceed 1000 characters.", nameof(reason));
        return normalized;
    }
}

public enum RegistrationType
{
    Individual = 0,
    Team = 1,
    Pair = 2,
    Relay = 3,
    Group = 4
}

public enum RegistrationStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Waitlisted = 5,
    Withdrawn = 6,
    Confirmed = 7,
    Cancelled = 8,
    NoShow = 9
}
