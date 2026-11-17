namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>A person's role profile kinds (S06 D12; glossary).</summary>
public enum ProfileKind
{
    Athlete,
    Staff,
    Official,
}

/// <summary>What the person's status moves need of every profile kind (<see cref="ProfileTransitions"/>).</summary>
internal interface IRoleProfile
{
    Guid Id { get; }

    ProfileStatus Status { get; }

    void MoveTo(ProfileStatus status);
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
