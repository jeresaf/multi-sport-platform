using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Domain.Entitlements;

/// <summary>
/// A tenant's entitlement overrides and the rules between them (S04 D5): at most one unremoved override per entitlement,
/// and replacing one removes it in the same change. Load every override of the tenant under the tenant's override lock
/// before a change, so two grants of one entitlement can't both add a row.
/// </summary>
public sealed class TenantEntitlementOverrides
{
    /// <summary>The remove reason of an override that a new grant of the same entitlement replaces.</summary>
    public const string ReplacedReason = "Replaced by a new override";

    private readonly List<EntitlementOverride> _overrides;

    public TenantEntitlementOverrides(IEnumerable<EntitlementOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        _overrides = [.. overrides];
    }

    /// <summary>Unremoved first, then the one created last first (UUIDv7).</summary>
    public IReadOnlyList<EntitlementOverride> Overrides =>
        [.. _overrides.OrderBy(o => o.IsRemoved).ThenByDescending(o => o.Id)];

    /// <summary>The unremoved overrides that apply on <paramref name="today"/>, by entitlement code.</summary>
    public IReadOnlyDictionary<string, EntitlementOverride> InEffectOn(DateOnly today) =>
        _overrides.Where(o => o.IsInEffectOn(today)).ToDictionary(o => o.EntitlementCode, StringComparer.Ordinal);

    /// <summary>
    /// <c>entitlement.override</c>: a new override of <paramref name="definition"/>, refused with
    /// <c>TENANT_OVERRIDE_INVALID_VALUE</c> if the value isn't of its type (or one of its levels). An unremoved override of
    /// the same entitlement is removed (<see cref="ReplacedReason"/>) and returned as replaced.
    /// </summary>
    public Result<OverrideGrant> Grant(
        EntitlementDefinition definition,
        EntitlementValue value,
        string reason,
        DateOnly? effectiveUntil,
        ICurrentActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);
        if (!definition.Accepts(value))
        {
            return TenancyErrors.OverrideInvalidValue;
        }

        var replaced = _overrides.Find(o => !o.IsRemoved && string.Equals(o.EntitlementCode, definition.Code, StringComparison.Ordinal));
        replaced?.Remove(ReplacedReason, actor, now);

        var granted = EntitlementOverride.Create(definition.Code, value, reason, effectiveUntil);
        _overrides.Add(granted);
        return Result.Success(new OverrideGrant(granted, replaced));
    }

    /// <summary><c>entitlement.override.remove</c>: removes the unremoved override <paramref name="overrideId"/> now.</summary>
    public Result<EntitlementOverride> Remove(Guid overrideId, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        if (_overrides.Find(o => o.Id == overrideId) is not { IsRemoved: false } removed)
        {
            return TenancyErrors.OverrideNotFound;
        }

        removed.Remove(reason, actor, now);
        return Result.Success(removed);
    }
}

/// <summary>What a grant did: the new override, and the one it replaced, if any.</summary>
public sealed record OverrideGrant(EntitlementOverride Granted, EntitlementOverride? Replaced);
