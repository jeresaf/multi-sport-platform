namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>The person lifecycle's transitions in S06 (D9); merging (<c>MERGED</c>) comes with S25.</summary>
public enum PersonTransition
{
    Deactivate,
    Reactivate,
    Archive,
    Unarchive,
    MarkDeceased,
}

/// <summary>
/// The person state machine as built (S06 D9; docs/domain/state-machines.md, Registry): <c>ACTIVE ⇄ INACTIVE</c>;
/// <c>ACTIVE | INACTIVE → ARCHIVED</c> and back to <c>ACTIVE</c>; <c>ACTIVE | INACTIVE → DECEASED</c>, final in S06.
/// Archiving, unarchiving and marking deceased need a reason; deactivating and reactivating take an optional one
/// (owner, review #99 Q1).
/// </summary>
public static class PersonTransitions
{
    /// <summary>The status <paramref name="transition"/> leads to from <paramref name="from"/>, or null where it isn't allowed.</summary>
    public static PersonStatus? Target(PersonTransition transition, PersonStatus from) => (transition, from) switch
    {
        (PersonTransition.Deactivate, PersonStatus.Active) => PersonStatus.Inactive,
        (PersonTransition.Reactivate, PersonStatus.Inactive) => PersonStatus.Active,
        (PersonTransition.Archive, PersonStatus.Active or PersonStatus.Inactive) => PersonStatus.Archived,
        (PersonTransition.Unarchive, PersonStatus.Archived) => PersonStatus.Active,
        (PersonTransition.MarkDeceased, PersonStatus.Active or PersonStatus.Inactive) => PersonStatus.Deceased,
        _ => null,
    };

    public static bool NeedsReason(PersonTransition transition) =>
        transition is PersonTransition.Archive or PersonTransition.Unarchive or PersonTransition.MarkDeceased;
}
