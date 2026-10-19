namespace SportsPlatform.Modules.Tenancy.Domain.Domains;

/// <summary>The tenant domain state machine (docs/domain/state-machines.md): only <c>ACTIVE</c> domains resolve.</summary>
public enum TenantDomainStatus
{
    PendingVerification,
    Active,
    Disabled,
}
