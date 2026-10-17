namespace SportsPlatform.BuildingBlocks.Tenancy;

/// <summary>The tenant of the current work, resolved from authentication, host or job payload (never the body).</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant => TenantId is not null;
}
