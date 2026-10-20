namespace SportsPlatform.Modules.Identity.Domain.Memberships;

/// <summary>A tenant membership's status (docs/domain/state-machines.md, "Tenant membership").</summary>
public enum MembershipStatus
{
    /// <summary>The member works in the tenant with the roles they hold.</summary>
    Active,

    /// <summary>Kept with its roles, but grants nothing until restored.</summary>
    Suspended,

    /// <summary>Terminal: rejoining the tenant creates a new membership.</summary>
    Removed,
}
