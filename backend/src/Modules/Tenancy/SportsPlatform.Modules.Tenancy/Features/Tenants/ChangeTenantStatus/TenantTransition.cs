namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;

/// <summary>The tenant state machine's transitions (docs/domain/state-machines.md, "Tenant").</summary>
public enum TenantTransition
{
    Activate,
    Suspend,
    Restore,
    Archive,
    Unarchive,
}
