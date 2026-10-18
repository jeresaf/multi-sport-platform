namespace SportsPlatform.Modules.Tenancy.Contracts.Tenants;

/// <summary>A tenant's stored lifecycle status (docs/domain/state-machines.md, "Tenant").</summary>
public enum TenantStatus
{
    /// <summary>Created and being set up; the public site is hidden.</summary>
    Provisioning,

    Active,

    /// <summary>Blocked by the platform, with a reason; can be restored.</summary>
    Suspended,

    /// <summary>Closed, with a reason. The tenant's data and slug are kept; the platform can unarchive it.</summary>
    Archived,
}
