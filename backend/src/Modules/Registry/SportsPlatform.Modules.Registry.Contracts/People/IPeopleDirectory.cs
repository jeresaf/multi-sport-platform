namespace SportsPlatform.Modules.Registry.Contracts.People;

/// <summary>A person's stored states (docs/domain/state-machines.md, Registry).</summary>
public enum PersonStatus
{
    Active,
    Inactive,
    Deceased,
    Archived,
}

/// <summary>
/// Whether a person is an adult on the tenant's date (S06 D13): computed from the date of birth on every read, never
/// stored. <see cref="Unknown"/> means no date of birth is recorded; such a person is treated as a minor wherever data
/// is shown publicly.
/// </summary>
public enum AgeStatus
{
    Adult,
    Minor,
    Unknown,
}

/// <summary>What another module may show of a person: no name parts, no date of birth.</summary>
public sealed record PersonSummary(Guid Id, string DisplayName, PersonStatus Status, AgeStatus AgeStatus);

/// <summary>
/// The people of the scope's tenant by ID, for modules that point at people (teams, registrations; S06 D22). Call it
/// after the scope's tenant is set. The caller checks its own action; this read checks none and is not audited.
/// </summary>
public interface IPeopleDirectory
{
    /// <summary>
    /// The summaries of the people among <paramref name="personIds"/> that exist in the scope's tenant, in no
    /// particular order; unknown IDs and other tenants' people are left out.
    /// </summary>
    Task<IReadOnlyList<PersonSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
}
