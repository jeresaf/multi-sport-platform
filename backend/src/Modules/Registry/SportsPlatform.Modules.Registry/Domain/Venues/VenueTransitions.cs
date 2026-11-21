namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// The venue state machine as built (S07 D12; docs/domain/state-machines.md, Registry): <c>ACTIVE ⇄
/// TEMPORARILY_UNAVAILABLE</c>; either → <c>CLOSED</c>; <c>CLOSED → ACTIVE</c> (reopen); any state but
/// <c>ARCHIVED</c> → <c>ARCHIVED</c>; <c>ARCHIVED</c> → only the status it had before archiving (unarchive, Q55), so
/// archiving a closed venue and unarchiving it never reopens it. Every change needs a reason.
/// </summary>
public static class VenueTransitions
{
    /// <summary>
    /// Whether a venue may move from <paramref name="from"/> to <paramref name="to"/>; <paramref name="beforeArchive"/>
    /// is the status an archived venue had before it was archived (ignored otherwise).
    /// </summary>
    public static bool IsAllowed(VenueStatus from, VenueStatus to, VenueStatus? beforeArchive = null) => (from, to) switch
    {
        (VenueStatus.Archived, _) => beforeArchive is { } before && before != VenueStatus.Archived && to == before,
        (VenueStatus.Active, VenueStatus.TemporarilyUnavailable or VenueStatus.Closed or VenueStatus.Archived) => true,
        (VenueStatus.TemporarilyUnavailable, VenueStatus.Active or VenueStatus.Closed or VenueStatus.Archived) => true,
        (VenueStatus.Closed, VenueStatus.Active or VenueStatus.Archived) => true,
        _ => false,
    };
}
