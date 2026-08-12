using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Subscriptions.Entities;

public class Subscription : TenantAggregateRoot<Guid>
{
    public new Guid TenantId { get; private set; }
    public string Plan { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public DateTime? GracePeriodEnd { get; private set; }
    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Draft;
    public bool AutoRenew { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    // Feature entitlements stored as JSON
    public string FeatureEntitlements { get; private set; } = "{}";
    public string UsageLimits { get; private set; } = "{}";

    private Subscription() { }

    public Subscription(Guid id, Guid tenantId, string plan, DateTime startDate, DateTime endDate)
        : base(id, tenantId)
    {
        TenantId = tenantId;
        Plan = plan;
        StartDate = startDate;
        EndDate = endDate;
        Status = SubscriptionStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates state transitions for the lease lifecycle state machine.
    /// DRAFT → ACTIVE → EXPIRING → GRACE_PERIOD → READ_ONLY → ARCHIVED
    /// Any state → SUSPENDED → ACTIVE (if unsuspended)
    /// </summary>
    private static readonly Dictionary<SubscriptionStatus, HashSet<SubscriptionStatus>> AllowedTransitions = new()
    {
        [SubscriptionStatus.Draft] = new() { SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatus.Archived },
        [SubscriptionStatus.Active] = new() { SubscriptionStatus.Expiring, SubscriptionStatus.GracePeriod, SubscriptionStatus.Suspended, SubscriptionStatus.Archived },
        [SubscriptionStatus.Expiring] = new() { SubscriptionStatus.GracePeriod, SubscriptionStatus.ReadOnly, SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatus.Archived },
        [SubscriptionStatus.GracePeriod] = new() { SubscriptionStatus.Active, SubscriptionStatus.ReadOnly, SubscriptionStatus.Suspended, SubscriptionStatus.Archived },
        [SubscriptionStatus.ReadOnly] = new() { SubscriptionStatus.Archived, SubscriptionStatus.Active },
        [SubscriptionStatus.Suspended] = new() { SubscriptionStatus.Active, SubscriptionStatus.Archived },
        [SubscriptionStatus.Archived] = new() { }, // Terminal state - no transitions allowed
    };

    private void TransitionTo(SubscriptionStatus newStatus)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {newStatus}. " +
                $"Allowed transitions from {Status}: [{string.Join(", ", allowed ?? new HashSet<SubscriptionStatus>())}]");
        }
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status == SubscriptionStatus.Draft || Status == SubscriptionStatus.Suspended || Status == SubscriptionStatus.Expiring)
        {
            TransitionTo(SubscriptionStatus.Active);
        }
        else
        {
            TransitionTo(SubscriptionStatus.Active);
        }
    }

    public void MarkExpiring()
    {
        TransitionTo(SubscriptionStatus.Expiring);
    }

    public void SetGracePeriod(DateTime graceEnd)
    {
        TransitionTo(SubscriptionStatus.GracePeriod);
        GracePeriodEnd = graceEnd;
    }

    public void SetReadOnly()
    {
        TransitionTo(SubscriptionStatus.ReadOnly);
    }

    public void Suspend()
    {
        TransitionTo(SubscriptionStatus.Suspended);
    }

    public void Unsuspend()
    {
        if (Status != SubscriptionStatus.Suspended)
            throw new InvalidOperationException($"Cannot unsuspend: current status is {Status}, expected Suspended.");
        // Restore to Active if within valid period, otherwise to ReadOnly
        if (DateTime.UtcNow <= EndDate)
        {
            Status = SubscriptionStatus.Active;
        }
        else if (GracePeriodEnd.HasValue && DateTime.UtcNow <= GracePeriodEnd.Value)
        {
            Status = SubscriptionStatus.GracePeriod;
        }
        else
        {
            Status = SubscriptionStatus.ReadOnly;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        TransitionTo(SubscriptionStatus.Archived);
    }

    public void ConfigureFeatures(string featureEntitlements, string usageLimits)
    {
        FeatureEntitlements = featureEntitlements;
        UsageLimits = usageLimits;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAutoRenew(bool autoRenew)
    {
        AutoRenew = autoRenew;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Renew(DateTime newEndDate)
    {
        var normalizedEndDate = newEndDate.Kind == DateTimeKind.Utc
            ? newEndDate
            : newEndDate.ToUniversalTime();
        if (normalizedEndDate <= DateTime.UtcNow)
            throw new ArgumentOutOfRangeException(
                nameof(newEndDate),
                "A renewed lease must end in the future.");
        if (Status == SubscriptionStatus.Archived)
            throw new InvalidOperationException(
                "An archived lease cannot be renewed.");

        EndDate = normalizedEndDate;
        GracePeriodEnd = null;
        Status = SubscriptionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActive()
    {
        return Status == SubscriptionStatus.Active && DateTime.UtcNow <= EndDate;
    }

    public bool CanWrite()
    {
        return Status == SubscriptionStatus.Active ||
               Status == SubscriptionStatus.Expiring ||
               Status == SubscriptionStatus.GracePeriod;
    }

    public bool IsInReadOnlyMode()
    {
        return Status == SubscriptionStatus.ReadOnly || Status == SubscriptionStatus.Archived;
    }

    public bool IsTerminal()
    {
        return Status == SubscriptionStatus.Archived;
    }
}

public enum SubscriptionStatus
{
    Draft,
    Active,
    Expiring,
    GracePeriod,
    ReadOnly,
    Suspended,
    Archived
}
