using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Subscriptions.Entities;

public class SubscriptionPlan : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "USD";
    public BillingInterval Interval { get; private set; } = BillingInterval.Monthly;
    public int TrialDays { get; private set; }
    public int MaxTenants { get; private set; } = 1;
    public int MaxUsers { get; private set; } = 10;
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<SubscriptionFeature> _features = new();
    public IReadOnlyCollection<SubscriptionFeature> Features => _features.AsReadOnly();

    private SubscriptionPlan() { }

    public SubscriptionPlan(Guid id, string name, string code, decimal price, BillingInterval interval)
        : base(id)
    {
        Name = name;
        Code = code;
        Price = price;
        Interval = interval;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, decimal price, string currency, BillingInterval interval)
    {
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        Interval = interval;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLimits(int maxTenants, int maxUsers, int trialDays)
    {
        MaxTenants = maxTenants;
        MaxUsers = maxUsers;
        TrialDays = trialDays;
        UpdatedAt = DateTime.UtcNow;
    }

    public SubscriptionFeature AddFeature(string code, string name, string? description, bool isEnabled, string? value)
    {
        var feature = new SubscriptionFeature(Guid.NewGuid(), Id, code, name, description, isEnabled, value);
        _features.Add(feature);
        UpdatedAt = DateTime.UtcNow;
        return feature;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum BillingInterval
{
    Monthly,
    Quarterly,
    SemiAnnually,
    Annually,
    OneTime
}
