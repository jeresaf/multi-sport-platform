using System.Collections.Frozen;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Domain.Plans;

/// <summary>
/// A plan as code declares it (S04 D2): what <c>tenancy.configuration.plans</c> seeds and the entitlement catalogue
/// exports. Every plan gives every entitlement a value (the Host checks at startup); a value changes only by a reviewed
/// change to <see cref="TenancyPlans"/>.
/// </summary>
public sealed class PlanDefinition
{
    public PlanDefinition(string code, string name, int rank, IReadOnlyDictionary<string, EntitlementValue> values)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        if (!EntitlementDefinition.IsValidCode(code))
        {
            throw new ArgumentException($"Plan code '{code}' must be UPPER_SNAKE_CASE, e.g. STANDARD.", nameof(code));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 1);

        Code = code;
        Name = name;
        Rank = rank;
        Values = values.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Stable, e.g. <c>STANDARD</c>.</summary>
    public string Code { get; }

    public string Name { get; }

    /// <summary>Orders plans, lowest first: an immediate plan change may only go up (S04 D4).</summary>
    public int Rank { get; }

    /// <summary>Each entitlement's value on this plan, by entitlement code.</summary>
    public IReadOnlyDictionary<string, EntitlementValue> Values { get; }
}
