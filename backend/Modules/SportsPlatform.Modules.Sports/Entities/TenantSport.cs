using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Sports.Entities;

public sealed class TenantSport : TenantAggregateRoot<Guid>
{
    public Guid SportId { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? EnabledAt { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private TenantSport() { }

    public TenantSport(Guid id, Guid tenantId, Guid sportId) : base(id, tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (sportId == Guid.Empty) throw new ArgumentException("Sport is required.", nameof(sportId));
        SportId = sportId;
        CreatedAt = DateTime.UtcNow;
        Enable();
    }

    public void Enable()
    {
        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        EnabledAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
