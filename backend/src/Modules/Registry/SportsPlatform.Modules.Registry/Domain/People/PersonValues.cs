namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>A person's stored states in S06 (docs/domain/state-machines.md, Registry; <c>MERGED</c> comes with merges, S25).</summary>
public enum PersonStatus
{
    Active,
    Inactive,
    Deceased,
    Archived,
}

/// <summary>Recorded for competition categories later; a person without one has none recorded (S06 D2).</summary>
public enum Gender
{
    Female,
    Male,
}

/// <summary>Where a record came from (AR-22). Imports add their own value with Data Exchange.</summary>
public enum PersonSource
{
    Manual,
    Seed,
}

/// <summary>What an alias is (S06 D10).</summary>
public enum AliasKind
{
    FormerName,
    Nickname,
    Spelling,
}

/// <summary>What an external identifier names. S07 adds venues (T1c), organisations (T2d), then teams.</summary>
public enum ExternalIdentifierEntityType
{
    Person,
    Venue,
    Organisation,
}
