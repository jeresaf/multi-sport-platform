namespace SportsPlatform.Modules.Contests.Entities;

/// <summary>
/// Public/application-facing contest lifecycle. Result approval and publication
/// are intentionally excluded; those belong to the Results module.
/// </summary>
public enum CanonicalContestStatus
{
    NotStarted,
    StartingSoon,
    Live,
    Interrupted,
    Suspended,
    Postponed,
    Ended,
    Cancelled,
    Abandoned
}

/// <summary>
/// Compatibility projection from the legacy ContestStatus enum to the canonical
/// contest lifecycle. This lets API clients migrate without an immediate,
/// destructive database enum rewrite.
/// </summary>
public static class ContestLifecycleProjection
{
    public static CanonicalContestStatus ToCanonical(this ContestStatus status) => status switch
    {
        ContestStatus.Draft => CanonicalContestStatus.NotStarted,
        ContestStatus.Scheduled => CanonicalContestStatus.NotStarted,
        ContestStatus.CheckInOpen => CanonicalContestStatus.NotStarted,
        ContestStatus.Ready => CanonicalContestStatus.StartingSoon,
        ContestStatus.InProgress => CanonicalContestStatus.Live,
        ContestStatus.Paused => CanonicalContestStatus.Interrupted,
        ContestStatus.Suspended => CanonicalContestStatus.Suspended,
        ContestStatus.Completed => CanonicalContestStatus.Ended,

        // Legacy result-workflow states are projected as an ended contest.
        // Approval/publication/correction are exposed separately by the Results module.
        ContestStatus.PendingApproval => CanonicalContestStatus.Ended,
        ContestStatus.Approved => CanonicalContestStatus.Ended,
        ContestStatus.Corrected => CanonicalContestStatus.Ended,
        ContestStatus.Published => CanonicalContestStatus.Ended,

        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,
        ContestStatus.Abandoned => CanonicalContestStatus.Abandoned,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown contest status.")
    };

    public static string ToApiCode(this CanonicalContestStatus status) => status switch
    {
        CanonicalContestStatus.NotStarted => "NOT_STARTED",
        CanonicalContestStatus.StartingSoon => "STARTING_SOON",
        CanonicalContestStatus.Live => "LIVE",
        CanonicalContestStatus.Interrupted => "INTERRUPTED",
        CanonicalContestStatus.Suspended => "SUSPENDED",
        CanonicalContestStatus.Postponed => "POSTPONED",
        CanonicalContestStatus.Ended => "ENDED",
        CanonicalContestStatus.Cancelled => "CANCELLED",
        CanonicalContestStatus.Abandoned => "ABANDONED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown canonical contest status.")
    };

    public static bool IsLegacyResultWorkflowState(this ContestStatus status) => status is
        ContestStatus.PendingApproval
        or ContestStatus.Approved
        or ContestStatus.Corrected
        or ContestStatus.Published;
}
