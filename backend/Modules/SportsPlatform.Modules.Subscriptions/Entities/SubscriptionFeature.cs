using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Subscriptions.Entities;

public class SubscriptionFeature : Entity<Guid>
{
    public Guid SubscriptionPlanId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? Value { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private SubscriptionFeature() { }

    public SubscriptionFeature(Guid id, Guid subscriptionPlanId, string code, string name, string? description, bool isEnabled, string? value)
        : base(id)
    {
        SubscriptionPlanId = subscriptionPlanId;
        Code = code;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        Value = value;
        CreatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateValue(string? value)
    {
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }
}
