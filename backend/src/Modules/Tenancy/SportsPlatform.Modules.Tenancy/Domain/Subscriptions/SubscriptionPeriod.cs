using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

/// <summary>
/// One subscription period of a tenant (S04 D3): a plan from <see cref="StartsOn"/> to <see cref="EndsOn"/>, both days
/// included, in the tenant's time zone. Tenant-owned (RLS). A row never changes except to be cancelled; renewing or
/// changing the plan adds a new row, so the history is never overwritten. Its status is derived from the dates on a
/// given day (<see cref="StatusOn"/>) and never stored. Who created it is in the audit columns.
/// </summary>
public sealed class SubscriptionPeriod : Entity, ITenantOwned
{
    public const int PlanCodeMaxLength = 40;
    public const int CancelReasonMaxLength = 500;
    public const int MaxWindowDays = 365;
    public const int DefaultExpiringWindowDays = 30;
    public const int DefaultGraceDays = 14;

    private SubscriptionPeriod(
        string planCode, SubscriptionKind kind, DateOnly startsOn, DateOnly endsOn, int expiringWindowDays, int graceDays)
        : base(NewId())
    {
        PlanCode = planCode;
        Kind = kind;
        StartsOn = startsOn;
        EndsOn = endsOn;
        ExpiringWindowDays = expiringWindowDays;
        GraceDays = graceDays;
    }

    private SubscriptionPeriod()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public SubscriptionKind Kind { get; private set; }

    public DateOnly StartsOn { get; private set; }

    /// <summary>The last day included.</summary>
    public DateOnly EndsOn { get; private set; }

    /// <summary>The status is <see cref="SubscriptionStatus.Expiring"/> for this many days before it ends, the last included.</summary>
    public int ExpiringWindowDays { get; private set; }

    /// <summary>The status is <see cref="SubscriptionStatus.Grace"/> for this many days after it ends.</summary>
    public int GraceDays { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public Guid? CancelledBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? CancelReason { get; private set; }

    public bool IsCancelled => CancelledAt is not null;

    /// <summary>The first day of <see cref="SubscriptionStatus.Expiring"/>.</summary>
    public DateOnly ExpiringFrom => EndsOn.AddDays(1 - ExpiringWindowDays);

    /// <summary>The last day of <see cref="SubscriptionStatus.Grace"/> (<see cref="EndsOn"/> without grace days).</summary>
    public DateOnly GraceEndsOn => EndsOn.AddDays(GraceDays);

    /// <summary>A new period; the request's validator has checked the dates and windows.</summary>
    public static SubscriptionPeriod Create(
        string planCode, SubscriptionKind kind, DateOnly startsOn, DateOnly endsOn, int expiringWindowDays, int graceDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planCode);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(planCode.Length, PlanCodeMaxLength, nameof(planCode));
        ArgumentOutOfRangeException.ThrowIfLessThan(endsOn, startsOn, nameof(endsOn));
        ArgumentOutOfRangeException.ThrowIfNegative(expiringWindowDays);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expiringWindowDays, MaxWindowDays);
        ArgumentOutOfRangeException.ThrowIfNegative(graceDays);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(graceDays, MaxWindowDays);
        return new SubscriptionPeriod(planCode, kind, startsOn, endsOn, expiringWindowDays, graceDays);
    }

    /// <summary>
    /// The status on <paramref name="today"/> (the tenant's date), as state-machines.md's table reads, top row first. A
    /// cancellation takes effect when it is made, so a cancelled period is <see cref="SubscriptionStatus.Cancelled"/>
    /// on every day.
    /// </summary>
    public SubscriptionStatus StatusOn(DateOnly today)
    {
        if (IsCancelled)
        {
            return SubscriptionStatus.Cancelled;
        }

        if (today < StartsOn)
        {
            return SubscriptionStatus.Scheduled;
        }

        if (today < ExpiringFrom)
        {
            return SubscriptionStatus.Active;
        }

        if (today <= EndsOn)
        {
            return SubscriptionStatus.Expiring;
        }

        return today <= GraceEndsOn ? SubscriptionStatus.Grace : SubscriptionStatus.Expired;
    }

    /// <summary>Whether it shares a day with <paramref name="startsOn"/>–<paramref name="endsOn"/>; cancelled periods share none.</summary>
    public bool Overlaps(DateOnly startsOn, DateOnly endsOn) => !IsCancelled && StartsOn <= endsOn && startsOn <= EndsOn;

    /// <summary>Cancels it now, with a reason; a period is cancelled once.</summary>
    public void Cancel(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(actor);
        if (IsCancelled)
        {
            throw new InvalidOperationException($"Subscription period {Id} is already cancelled.");
        }

        CancelledAt = now;
        CancelledBy = actor.ActorId;
        CancelReason = reason.Trim();
    }
}
