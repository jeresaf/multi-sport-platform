namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>What a tenant may do, derived from its status (and, from S04, its subscription): state-machines.md.</summary>
public enum TenantAccessMode
{
    Full,

    /// <summary>Reads and exports only. Needs subscriptions (S04); not produced yet.</summary>
    ReadOnly,

    /// <summary>Admin access denied; the public site is off.</summary>
    Blocked,
}
