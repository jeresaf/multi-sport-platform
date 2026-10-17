using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>A settable tenant until S02 resolves tenants from the host and authentication.</summary>
public sealed class TestTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
