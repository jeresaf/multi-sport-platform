namespace SportsPlatform.BuildingBlocks;

public abstract class TenantEntity<TId> : Entity<TId>
    where TId : notnull
{
    public Guid TenantId { get; protected set; }

    protected TenantEntity() { }

    protected TenantEntity(TId id, Guid tenantId) : base(id)
    {
        TenantId = tenantId;
    }
}

public abstract class TenantAggregateRoot<TId> : AggregateRoot<TId>
    where TId : notnull
{
    public Guid TenantId { get; protected set; }

    protected TenantAggregateRoot() { }

    protected TenantAggregateRoot(TId id, Guid tenantId) : base(id)
    {
        TenantId = tenantId;
    }
}