namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// The gender part of a team's category (S07 D7, Q53). Generic codes: the web app labels them ("Men", or "Boys" with an
/// age group up to <see cref="AgeGroup.U17"/>), never a definition.
/// </summary>
public enum GenderCategory
{
    Men,
    Women,
    Mixed,
}

/// <summary>
/// The age part of a team's category (S07 D7, Q53). Descriptive only: an edition's age limits use its own rules and a
/// reference date (S09), not the team's age group.
/// </summary>
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

/// <summary>The level part of a team's category (S07 D7, Q53): <see cref="First"/> unless the request says otherwise.</summary>
public enum TeamLevel
{
    First,
    Reserve,
    Development,
    Academy,
}

/// <summary>A team's states (docs/domain/state-machines.md, Registry; S07 D12).</summary>
public enum TeamStatus
{
    Active,
    Inactive,
    Suspended,
    Disbanded,
    Archived,
}

/// <summary>Where a team record came from (AR-22). Imports add their own value with Data Exchange.</summary>
public enum TeamSource
{
    Manual,
    Seed,
}

/// <summary>A team's category (S07 D7): gender, age group and level, shown together ("Senior Men", "Under-17 Women").</summary>
public sealed record TeamCategory(GenderCategory Gender, AgeGroup AgeGroup, TeamLevel Level);
