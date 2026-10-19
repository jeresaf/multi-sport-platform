namespace SportsPlatform.Modules.Tenancy.Domain.Domains;

public enum TenantDomainType
{
    /// <summary><c>&lt;slug&gt;.&lt;Platform:BaseDomain&gt;</c>, created with the tenant.</summary>
    PlatformSubdomain,

    /// <summary>A domain the tenant owns; needs verification (Branding/PRO, not built yet).</summary>
    Custom,
}
