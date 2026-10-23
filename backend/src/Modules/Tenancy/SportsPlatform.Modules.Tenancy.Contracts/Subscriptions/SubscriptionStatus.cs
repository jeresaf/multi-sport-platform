namespace SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

/// <summary>
/// A subscription period's status, derived from its stored dates on a given day (docs/domain/state-machines.md,
/// "Subscription period"); never stored.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Starts after today.</summary>
    Scheduled,

    Active,

    /// <summary>Within the expiring window before it ends: still full access, with a banner.</summary>
    Expiring,

    /// <summary>Ended, within the grace days: still full access, with a banner.</summary>
    Grace,

    /// <summary>Ended and past its grace days.</summary>
    Expired,

    /// <summary>Cancelled by the platform, with a reason; takes effect when cancelled.</summary>
    Cancelled,
}
