namespace SportsPlatform.BuildingBlocks.Tenancy;

/// <summary>
/// The scoped <see cref="ITenantContext"/>. Empty for HTTP requests until S02 resolves tenants; the outbox processor
/// sets it from the event envelope before a consumer runs.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid? tenantId) => TenantId = tenantId;
}
