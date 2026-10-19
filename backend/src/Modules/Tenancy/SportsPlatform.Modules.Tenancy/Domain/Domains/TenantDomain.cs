using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Tenancy.Domain.Domains;

/// <summary>
/// A host name that serves a tenant's public site (<c>tenancy.tenant_domains</c>). Global, not tenant-owned: public
/// requests look it up before any tenant is known. <see cref="Host"/> is unique across all tenants; each tenant has at
/// most one primary domain.
/// </summary>
public sealed class TenantDomain : Entity
{
    private TenantDomain(Guid tenantId, string host, TenantDomainType type, bool isPrimary, TenantDomainStatus status)
        : base(NewId())
    {
        TenantId = tenantId;
        Host = host;
        Type = type;
        IsPrimary = isPrimary;
        Status = status;
    }

    private TenantDomain()
    {
    }

    public Guid TenantId { get; private set; }

    public string Host { get; private set; } = string.Empty;

    public TenantDomainType Type { get; private set; }

    public bool IsPrimary { get; private set; }

    public TenantDomainStatus Status { get; private set; }

    /// <summary>
    /// The tenant's platform subdomain, <c>&lt;slug&gt;.&lt;baseDomain&gt;</c>: primary and <c>ACTIVE</c> from the
    /// start, since the platform owns the base domain.
    /// </summary>
    public static TenantDomain PlatformSubdomain(Guid tenantId, string slug, string baseDomain)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant ID cannot be empty.", nameof(tenantId));
        }

        var host = $"{slug}.{baseDomain}";
        if (!TenantHost.IsValid(host))
        {
            throw new ArgumentException($"'{host}' is not a valid host name.", nameof(slug));
        }

        return new TenantDomain(tenantId, host, TenantDomainType.PlatformSubdomain, isPrimary: true, TenantDomainStatus.Active);
    }
}
