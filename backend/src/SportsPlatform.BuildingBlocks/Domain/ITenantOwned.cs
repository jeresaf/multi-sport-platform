namespace SportsPlatform.BuildingBlocks.Domain;

/// <summary>Marks tenant-owned data. The tenant is stamped from the tenant context, never from input (AR-04).</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
