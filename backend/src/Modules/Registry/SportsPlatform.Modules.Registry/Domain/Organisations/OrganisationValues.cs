namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>What kind of body an organisation is (S07 D4). Generic codes: none names a sport (AR-11).</summary>
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

/// <summary>An organisation's states (docs/domain/state-machines.md, Registry; S07 D12).</summary>
public enum OrganisationStatus
{
    Active,
    Inactive,
    Suspended,
    Dissolved,
    Archived,
}

/// <summary>Where an organisation record came from (AR-22). Imports add their own value with Data Exchange.</summary>
public enum OrganisationSource
{
    Manual,
    Seed,
}
