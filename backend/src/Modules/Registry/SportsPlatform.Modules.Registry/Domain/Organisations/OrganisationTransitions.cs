namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// The organisation state machine as built (S07 D12; docs/domain/state-machines.md, Registry): <c>ACTIVE</c>,
/// <c>INACTIVE</c> and <c>SUSPENDED</c> move freely between each other and to <c>DISSOLVED</c> or <c>ARCHIVED</c>;
/// <c>DISSOLVED</c> → only <c>ARCHIVED</c>; <c>ARCHIVED</c> → only the status it had before archiving (unarchive, Q55),
/// so archiving a dissolved organisation and unarchiving it never undoes the dissolution. Every change needs a reason.
/// An organisation's status never changes its teams' (D12).
/// </summary>
public static class OrganisationTransitions
{
    /// <summary>
    /// Whether an organisation may move from <paramref name="from"/> to <paramref name="to"/>;
    /// <paramref name="beforeArchive"/> is the status an archived organisation had before it was archived (ignored
    /// otherwise).
    /// </summary>
    public static bool IsAllowed(OrganisationStatus from, OrganisationStatus to, OrganisationStatus? beforeArchive = null) =>
        (from, to) switch
        {
            (OrganisationStatus.Archived, _) =>
                beforeArchive is { } before && before != OrganisationStatus.Archived && to == before,
            (OrganisationStatus.Dissolved, OrganisationStatus.Archived) => true,
            (OrganisationStatus.Dissolved, _) => false,
            (OrganisationStatus.Active or OrganisationStatus.Inactive or OrganisationStatus.Suspended, _) => from != to,
            _ => false,
        };
}
