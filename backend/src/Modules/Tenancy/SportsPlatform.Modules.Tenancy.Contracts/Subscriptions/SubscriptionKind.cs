namespace SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

/// <summary>How a period was granted; for reporting only, never affects its status or access (state-machines.md).</summary>
public enum SubscriptionKind
{
    Paid,

    Trial,

    Complimentary,
}
