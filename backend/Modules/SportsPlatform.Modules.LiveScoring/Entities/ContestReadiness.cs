using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.LiveScoring.Entities;

public sealed class ContestReadiness : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public bool RequireParticipantCheckIn { get; private set; }
    public bool RequireRosterConfirmation { get; private set; }
    public bool RequireOfficialConfirmation { get; private set; }
    public bool RequireVenueConfirmation { get; private set; }
    public bool RosterConfirmed { get; private set; }
    public bool VenueConfirmed { get; private set; }
    public Guid? OverrideBy { get; private set; }
    public string? OverrideReason { get; private set; }
    public DateTime? OverriddenAt { get; private set; }
    private ContestReadiness() { }

    public ContestReadiness(
        Guid id,
        Guid tenantId,
        Guid contestId,
        bool requireParticipantCheckIn,
        bool requireRosterConfirmation,
        bool requireOfficialConfirmation,
        bool requireVenueConfirmation)
        : base(id, tenantId)
    {
        ContestId = contestId;
        RequireParticipantCheckIn = requireParticipantCheckIn;
        RequireRosterConfirmation = requireRosterConfirmation;
        RequireOfficialConfirmation = requireOfficialConfirmation;
        RequireVenueConfirmation = requireVenueConfirmation;
        CreatedAt = DateTime.UtcNow;
    }

    public void ConfirmRoster()
    {
        RosterConfirmed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmVenue()
    {
        VenueConfirmed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Override(Guid actorId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An override reason is required.", nameof(reason));
        OverrideBy = actorId;
        OverrideReason = reason.Trim();
        OverriddenAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class ContestCheckInHistory : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string SubjectType { get; private set; } = string.Empty;
    public Guid? SubjectId { get; private set; }
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid ActorId { get; private set; }
    public string? Reason { get; private set; }
    public bool IsOverride { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ContestCheckInHistory() { }

    public ContestCheckInHistory(
        Guid id,
        Guid tenantId,
        Guid contestId,
        string action,
        string subjectType,
        Guid? subjectId,
        string? previousValue,
        string? newValue,
        Guid actorId,
        string? reason = null,
        bool isOverride = false)
        : base(id, tenantId)
    {
        ContestId = contestId;
        Action = action;
        SubjectType = subjectType;
        SubjectId = subjectId;
        PreviousValue = previousValue;
        NewValue = newValue;
        ActorId = actorId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        IsOverride = isOverride;
        OccurredAt = DateTime.UtcNow;
    }
}
