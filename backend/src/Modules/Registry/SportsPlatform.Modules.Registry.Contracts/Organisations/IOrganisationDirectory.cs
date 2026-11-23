namespace SportsPlatform.Modules.Registry.Contracts.Organisations;

/// <summary>What kind of body an organisation is (S07 D4).</summary>
public enum OrganisationType
{
    Club,
    School,
    University,
    Academy,
    Federation,
    Association,
    Company,
    Community,
    Government,
    Other,
}

/// <summary>An organisation's stored states (docs/domain/state-machines.md, Registry; S07 D12).</summary>
public enum OrganisationStatus
{
    Active,
    Inactive,
    Suspended,
    Dissolved,
    Archived,
}

/// <summary>
/// What another module may show of an organisation (S07 D18): its names, its type, its status and its logo (a Platform
/// media asset, null until it has one).
/// </summary>
public sealed record OrganisationSummary(
    Guid Id,
    string DisplayName,
    string? ShortName,
    OrganisationType Type,
    OrganisationStatus Status,
    Guid? LogoAssetId);

/// <summary>
/// The organisations of the scope's tenant by ID, for modules that point at organisations (teams' owners in other modules,
/// competitions' participants; S07 D18). Call it after the scope's tenant is set. The caller checks its own action; this
/// read checks none and is not audited.
/// </summary>
public interface IOrganisationDirectory
{
    /// <summary>
    /// The summaries of the organisations among <paramref name="organisationIds"/> that exist in the scope's tenant,
    /// whatever their status, in no particular order; unknown IDs and other tenants' organisations are left out.
    /// </summary>
    Task<IReadOnlyList<OrganisationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> organisationIds, CancellationToken cancellationToken);
}
