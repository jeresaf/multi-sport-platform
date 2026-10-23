using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Domain.Entitlements;

/// <summary>
/// A tenant's own value of one entitlement, in place of its plan's (S04 D5): an event pass's edition limit, a pilot's
/// extra staff seats. Tenant-owned (RLS). It applies until <see cref="EffectiveUntil"/> (the last day included, the
/// tenant's date; none = until removed). A row never changes except to be removed; replacing an override removes the old
/// row and adds a new one, so the history is kept (AR-18). Who granted it and when are in the audit columns. The reason
/// is for platform staff only: tenants see the value, never the reason.
/// </summary>
public sealed class EntitlementOverride : Entity, ITenantOwned
{
    public const int EntitlementCodeMaxLength = 50;
    public const int ReasonMaxLength = 500;

    private EntitlementOverride(string entitlementCode, EntitlementValue value, string reason, DateOnly? effectiveUntil)
        : base(NewId())
    {
        EntitlementCode = entitlementCode;
        Value = value;
        Reason = reason;
        EffectiveUntil = effectiveUntil;
    }

    private EntitlementOverride()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>An entitlement code from the catalogue, e.g. <c>MAX_ACTIVE_EDITIONS</c>.</summary>
    public string EntitlementCode { get; private set; } = string.Empty;

    /// <summary>Of the entitlement's type (and one of its levels).</summary>
    public EntitlementValue Value { get; private set; } = EntitlementValue.Flag(false);

    /// <summary>Why it was granted: free text kept permanently, never personal data, never shown to the tenant.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>The last day it applies, in the tenant's time zone; null until it is removed.</summary>
    public DateOnly? EffectiveUntil { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? RemoveReason { get; private set; }

    public bool IsRemoved => RemovedAt is not null;

    /// <summary>A new override; the definition has accepted the value and the reason is 1–500 characters.</summary>
    public static EntitlementOverride Create(string entitlementCode, EntitlementValue value, string reason, DateOnly? effectiveUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementCode);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(entitlementCode.Length, EntitlementCodeMaxLength, nameof(entitlementCode));
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new EntitlementOverride(entitlementCode, value, reason.Trim(), effectiveUntil);
    }

    /// <summary>Whether it applies on <paramref name="today"/> (the tenant's date): not removed and not past its last day.</summary>
    public bool IsInEffectOn(DateOnly today) => !IsRemoved && (EffectiveUntil is not { } until || today <= until);

    /// <summary>Removes it now, with a reason; an override is removed once.</summary>
    public void Remove(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(actor);
        if (IsRemoved)
        {
            throw new InvalidOperationException($"Entitlement override {Id} is already removed.");
        }

        RemovedAt = now;
        RemovedBy = actor.ActorId;
        RemoveReason = reason.Trim();
    }
}
