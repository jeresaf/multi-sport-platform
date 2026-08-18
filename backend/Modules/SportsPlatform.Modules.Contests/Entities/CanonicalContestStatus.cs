namespace SportsPlatform.Modules.Contests.Entities;

/// <summary>
/// Stable, sport-neutral lifecycle exposed by public contracts. The persisted
/// <see cref="ContestStatus"/> remains temporarily available during migration.
/// </summary>
public enum CanonicalContestStatus
{
    NotStarted,
    StartingSoon,
    Live,
    Delayed,
    Interrupted,
    Suspended,
    Postponed,
    Ended,
    Closed,
    Cancelled,
    Abandoned
}

public sealed record ContestPhase(
    string Code,
    string DisplayName,
    int? Sequence,
    string? Clock,
    bool IsBreak,
    bool IsOvertime);

public static class CanonicalContestStatusExtensions
{
    public static CanonicalContestStatus ToCanonical(this ContestStatus status) => status switch
    {
        ContestStatus.Draft or ContestStatus.Scheduled or ContestStatus.CheckInOpen
            => CanonicalContestStatus.NotStarted,
        ContestStatus.Ready => CanonicalContestStatus.StartingSoon,
        ContestStatus.InProgress => CanonicalContestStatus.Live,
        ContestStatus.Paused => CanonicalContestStatus.Interrupted,
        ContestStatus.Suspended => CanonicalContestStatus.Suspended,
        ContestStatus.Completed or ContestStatus.PendingApproval
            => CanonicalContestStatus.Ended,
        ContestStatus.Approved or ContestStatus.Corrected or ContestStatus.Published
            => CanonicalContestStatus.Closed,
        ContestStatus.Cancelled => CanonicalContestStatus.Cancelled,
        ContestStatus.Abandoned => CanonicalContestStatus.Abandoned,
        _ => CanonicalContestStatus.NotStarted
    };

    public static string ToCode(this CanonicalContestStatus status) => status switch
    {
        CanonicalContestStatus.NotStarted => "NOT_STARTED",
        CanonicalContestStatus.StartingSoon => "STARTING_SOON",
        CanonicalContestStatus.Live => "LIVE",
        CanonicalContestStatus.Delayed => "DELAYED",
        CanonicalContestStatus.Interrupted => "INTERRUPTED",
        CanonicalContestStatus.Suspended => "SUSPENDED",
        CanonicalContestStatus.Postponed => "POSTPONED",
        CanonicalContestStatus.Ended => "ENDED",
        CanonicalContestStatus.Closed => "CLOSED",
        CanonicalContestStatus.Cancelled => "CANCELLED",
        CanonicalContestStatus.Abandoned => "ABANDONED",
        _ => "NOT_STARTED"
    };

    public static bool TryParseProviderStatus(
        string? value,
        out CanonicalContestStatus status)
    {
        var normalized = value?.Trim().Replace('-', '_').Replace(' ', '_')
            .ToUpperInvariant();
        status = normalized switch
        {
            "NOT_STARTED" or "SCHEDULED" or "DRAFT" => CanonicalContestStatus.NotStarted,
            "STARTING_SOON" or "READY" => CanonicalContestStatus.StartingSoon,
            "LIVE" or "IN_PROGRESS" or "INPROGRESS" => CanonicalContestStatus.Live,
            "DELAYED" => CanonicalContestStatus.Delayed,
            "INTERRUPTED" or "PAUSED" => CanonicalContestStatus.Interrupted,
            "SUSPENDED" => CanonicalContestStatus.Suspended,
            "POSTPONED" => CanonicalContestStatus.Postponed,
            "ENDED" or "COMPLETED" => CanonicalContestStatus.Ended,
            "CLOSED" or "PUBLISHED" or "APPROVED" => CanonicalContestStatus.Closed,
            "CANCELLED" or "CANCELED" => CanonicalContestStatus.Cancelled,
            "ABANDONED" => CanonicalContestStatus.Abandoned,
            _ => CanonicalContestStatus.NotStarted
        };
        return normalized is "NOT_STARTED" or "SCHEDULED" or "DRAFT"
            or "STARTING_SOON" or "READY" or "LIVE" or "IN_PROGRESS"
            or "INPROGRESS" or "DELAYED" or "INTERRUPTED" or "PAUSED"
            or "SUSPENDED" or "POSTPONED" or "ENDED" or "COMPLETED"
            or "CLOSED" or "PUBLISHED" or "APPROVED" or "CANCELLED"
            or "CANCELED" or "ABANDONED";
    }
}
