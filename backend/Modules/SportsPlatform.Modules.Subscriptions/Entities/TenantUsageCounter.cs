using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Subscriptions.Entities;

/// <summary>
/// Durable tenant usage that cannot be derived from the current domain state.
/// PeriodStartUtc is the beginning of the measurement bucket (hour or month).
/// </summary>
public sealed class TenantUsageCounter : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Metric { get; private set; } = string.Empty;
    public DateTime PeriodStartUtc { get; private set; }
    public long Value { get; private set; }
    private TenantUsageCounter() { }

    public TenantUsageCounter(Guid id, Guid tenantId, string metric, DateTime periodStartUtc, long value = 0)
        : base(id)
    {
        TenantId = tenantId;
        Metric = metric;
        PeriodStartUtc = DateTime.SpecifyKind(periodStartUtc, DateTimeKind.Utc);
        Value = value;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Increment(long amount)
    {
        Value = Math.Max(0, Value + amount);
        UpdatedAt = DateTime.UtcNow;
    }
}
