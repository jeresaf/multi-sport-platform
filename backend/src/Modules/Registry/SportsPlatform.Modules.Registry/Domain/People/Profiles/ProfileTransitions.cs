namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>
/// The role profile state machine as built (S06 D12; owner, S06-T4 plan; docs/domain/state-machines.md, Registry):
/// <c>ACTIVE ⇄ INACTIVE</c>; <c>ACTIVE | INACTIVE → RETIRED</c>; <c>RETIRED → ACTIVE</c> (a comeback); any state but
/// <c>ARCHIVED</c> → <c>ARCHIVED</c>, which is final (for a profile recorded by mistake). Every change needs a reason.
/// A profile is open until it is archived: a person holds at most one open profile of a kind (per variant for an
/// athlete).
/// </summary>
public static class ProfileTransitions
{
    /// <summary>Whether a profile may move from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsAllowed(ProfileStatus from, ProfileStatus to) => (from, to) switch
    {
        (ProfileStatus.Active, ProfileStatus.Inactive) => true,
        (ProfileStatus.Inactive, ProfileStatus.Active) => true,
        (ProfileStatus.Active or ProfileStatus.Inactive, ProfileStatus.Retired) => true,
        (ProfileStatus.Retired, ProfileStatus.Active) => true,
        (ProfileStatus.Active or ProfileStatus.Inactive or ProfileStatus.Retired, ProfileStatus.Archived) => true,
        _ => false,
    };

    /// <summary>Whether a profile in <paramref name="status"/> is open: anything but <c>ARCHIVED</c>.</summary>
    public static bool IsOpen(ProfileStatus status) => status != ProfileStatus.Archived;
}
