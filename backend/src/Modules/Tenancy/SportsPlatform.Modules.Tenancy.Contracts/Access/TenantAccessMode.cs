namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>What a tenant may do, derived from its status and its current subscription period: state-machines.md.</summary>
public enum TenantAccessMode
{
    Full,

    /// <summary>
    /// Reads (and later exports and a few writes with their own window) only: an <c>ACTIVE</c> tenant whose
    /// subscription has expired, was cancelled or hasn't started, or that has none (S04 D8).
    /// </summary>
    ReadOnly,

    /// <summary>Admin access denied; the public site is off.</summary>
    Blocked,
}
