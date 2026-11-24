namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// The team state machine as built (S07 D12; docs/domain/state-machines.md, Registry): <c>ACTIVE</c>, <c>INACTIVE</c>
/// and <c>SUSPENDED</c> move freely between each other and to <c>DISBANDED</c> or <c>ARCHIVED</c>; <c>DISBANDED</c> →
/// <c>ACTIVE</c> (reform) or <c>ARCHIVED</c>; <c>ARCHIVED</c> → only the status it had before archiving (unarchive, Q55).
/// Every change needs a reason. Reform and an unarchive to a status that counts toward <c>MAX_TEAMS</c> take a place
/// of the quota (D14): the use case checks it.
/// </summary>
public static class TeamTransitions
{
    /// <summary>
    /// Whether a team may move from <paramref name="from"/> to <paramref name="to"/>; <paramref name="beforeArchive"/> is
    /// the status an archived team had before it was archived (ignored otherwise).
    /// </summary>
    public static bool IsAllowed(TeamStatus from, TeamStatus to, TeamStatus? beforeArchive = null) =>
        (from, to) switch
        {
            (TeamStatus.Archived, _) => beforeArchive is { } before && before != TeamStatus.Archived && to == before,
            (TeamStatus.Disbanded, TeamStatus.Active or TeamStatus.Archived) => true,
            (TeamStatus.Disbanded, _) => false,
            (TeamStatus.Active or TeamStatus.Inactive or TeamStatus.Suspended, _) => from != to,
            _ => false,
        };
}
