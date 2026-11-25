namespace SportsPlatform.Modules.Registry.Contracts.Teams;

/// <summary>The gender part of a team's category (S07 D7).</summary>
public enum GenderCategory
{
    Men,
    Women,
    Mixed,
}

/// <summary>The age part of a team's category (S07 D7, Q53). Descriptive only: an edition's age limits are its own (S09).</summary>
public enum AgeGroup
{
    Senior,
    U6,
    U7,
    U8,
    U9,
    U10,
    U11,
    U12,
    U13,
    U14,
    U15,
    U16,
    U17,
    U18,
    U19,
    U20,
    U21,
    U22,
    U23,
    Veterans,
}

/// <summary>The level part of a team's category (S07 D7, Q53).</summary>
public enum TeamLevel
{
    First,
    Reserve,
    Development,
    Academy,
}

/// <summary>A team's stored states (docs/domain/state-machines.md, Registry; S07 D12).</summary>
public enum TeamStatus
{
    Active,
    Inactive,
    Suspended,
    Disbanded,
    Archived,
}

/// <summary>
/// What another module may show of a team (S07 D18): its name, short name and code as they were on the date asked for
/// (the current ones without a date), its sport variant (a code), its category, its organisation (null when
/// independent), its status, its slug, and its logo and badge (Platform media assets, null until it has them).
/// </summary>
public sealed record TeamSummary(
    Guid Id,
    string Name,
    string? ShortName,
    string? Code,
    string Variant,
    GenderCategory GenderCategory,
    AgeGroup AgeGroup,
    TeamLevel Level,
    Guid? OrganisationId,
    TeamStatus Status,
    string Slug,
    Guid? LogoAssetId,
    Guid? BadgeAssetId);

/// <summary>
/// The teams of the scope's tenant by ID, for modules that point at teams (memberships, entries, fixtures and results;
/// S07 D18). Call it after the scope's tenant is set. The caller checks its own action; this read checks none and is not
/// audited.
/// </summary>
public interface ITeamDirectory
{
    /// <summary>
    /// The summaries of the teams among <paramref name="teamIds"/> that exist in the scope's tenant, whatever their
    /// status, in no particular order; unknown IDs and other tenants' teams are left out. With <paramref name="onDate"/>
    /// (a date in the tenant's calendar), each team's name, short name and code are the ones it had that day: on or
    /// after the day the current name began, the current ones; before it, the former name whose dates cover the date;
    /// before the first name began, the first name (S07 D8). Without it, the current ones.
    /// </summary>
    Task<IReadOnlyList<TeamSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> teamIds, DateOnly? onDate, CancellationToken cancellationToken);
}
