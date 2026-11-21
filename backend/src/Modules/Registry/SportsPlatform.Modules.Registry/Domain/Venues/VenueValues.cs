namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>What kind of place a venue is (S07 D3). Generic codes: none names a sport (AR-11).</summary>
public enum VenueType
{
    Stadium,
    Arena,
    SportsGround,
    Field,
    Court,
    IndoorHall,
    Track,
    MultiSportComplex,
    SchoolGround,
    Other,
}

/// <summary>A playing surface a venue has (S07 D3). Generic codes: none names a sport (AR-11).</summary>
public enum VenueSurface
{
    NaturalGrass,
    ArtificialTurf,
    HybridGrass,
    Hardwood,
    SyntheticCourt,
    Concrete,
    Clay,
    Sand,
    Other,
}

/// <summary>A venue's states (docs/domain/state-machines.md, Registry; S07 D12). The transitions come with S07-T1b.</summary>
public enum VenueStatus
{
    Active,
    TemporarilyUnavailable,
    Closed,
    Archived,
}

/// <summary>Where a venue record came from (AR-22). Imports add their own value with Data Exchange.</summary>
public enum VenueSource
{
    Manual,
    Seed,
}
