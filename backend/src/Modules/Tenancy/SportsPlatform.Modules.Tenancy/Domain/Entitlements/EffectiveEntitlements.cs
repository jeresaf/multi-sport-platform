using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Domain.Entitlements;

/// <summary>
/// A tenant's effective entitlements on one day (S04 D6): the definition's default, then the plan's value, then an
/// override in effect. The plan is the current period's, whatever its status (owner default, plan 2026-10-29): step 1
/// already refuses writes when it isn't in effect, and reads keep their plan's gates. A stored value that isn't of the
/// entitlement's type is ignored.
/// </summary>
public sealed class EffectiveEntitlements
{
    /// <summary>The share of a quota at which the tenant is warned (S04 D6).</summary>
    public const int WarningPercent = 80;

    private readonly string? _planCode;
    private readonly IReadOnlyDictionary<string, EntitlementValue> _planValues;
    private readonly IReadOnlyDictionary<string, EntitlementOverride> _overrides;

    /// <param name="planCode">The current period's plan; null without a period.</param>
    /// <param name="planValues">That plan's values by entitlement code (empty without a period).</param>
    /// <param name="overridesInEffect">The tenant's unremoved overrides in effect on the day, by entitlement code.</param>
    public EffectiveEntitlements(
        string? planCode,
        IReadOnlyDictionary<string, EntitlementValue> planValues,
        IReadOnlyDictionary<string, EntitlementOverride> overridesInEffect)
    {
        ArgumentNullException.ThrowIfNull(planValues);
        ArgumentNullException.ThrowIfNull(overridesInEffect);
        _planCode = planCode;
        _planValues = planValues;
        _overrides = overridesInEffect;
    }

    public EffectiveEntitlement Of(EntitlementDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_overrides.TryGetValue(definition.Code, out var entitlementOverride) && definition.Accepts(entitlementOverride.Value))
        {
            return new EffectiveEntitlement(definition.Code, entitlementOverride.Value, EntitlementSource.Override, _planCode);
        }

        return _planCode is not null && _planValues.TryGetValue(definition.Code, out var value) && definition.Accepts(value)
            ? new EffectiveEntitlement(definition.Code, value, EntitlementSource.Plan, _planCode)
            : new EffectiveEntitlement(definition.Code, definition.Default, EntitlementSource.Default, _planCode);
    }

    /// <summary>A boolean that is on, or a level entitlement at any level; never a quota.</summary>
    public static bool Allows(EntitlementValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Type switch
        {
            EntitlementType.Boolean => value.Enabled,
            EntitlementType.Level => value.LevelCode is not null,
            _ => throw new ArgumentException($"A {value.Type} entitlement is counted, not allowed.", nameof(value)),
        };
    }

    /// <summary>
    /// Whether one more item fits <paramref name="value"/>, a quota, with <paramref name="used"/> items before it:
    /// refused at the limit; a warning when the new item reaches <see cref="WarningPercent"/> % of it.
    /// </summary>
    public static Result<QuotaCheck> CheckQuota(EntitlementValue value, int used)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(used);
        if (value.Type != EntitlementType.Quota)
        {
            throw new ArgumentException($"A {value.Type} entitlement has no quota.", nameof(value));
        }

        if (value.Limit is not { } limit)
        {
            return Result.Success(new QuotaCheck(null, used, Warning: false));
        }

        if (used >= limit)
        {
            return TenancyErrors.QuotaExceeded;
        }

        return Result.Success(new QuotaCheck(limit, used, Warning: (long)(used + 1) * 100 >= (long)limit * WarningPercent));
    }
}
