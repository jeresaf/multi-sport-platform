namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>A person's role profile kinds (S06 D12; glossary). S06-T4b adds staff and official profiles.</summary>
public enum ProfileKind
{
    Athlete,
}

/// <summary>
/// A role profile's stored states in S06 (docs/domain/state-machines.md, Registry). <c>SUSPENDED</c> comes with
/// discipline (S13+).
/// </summary>
public enum ProfileStatus
{
    Active,
    Inactive,
    Retired,
    Archived,
}

/// <summary>An athlete's dominant side (S06 D12); none recorded is null.</summary>
public enum DominantSide
{
    Left,
    Right,
    Both,
}
