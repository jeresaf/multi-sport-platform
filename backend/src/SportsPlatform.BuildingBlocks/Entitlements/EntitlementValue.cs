using System.Globalization;
using System.Text.Json.Serialization;

namespace SportsPlatform.BuildingBlocks.Entitlements;

/// <summary>
/// The value of one entitlement, from a plan, a tenant override or the default: on or off for a boolean, a limit (or
/// unlimited) for a quota, a level (or none) for a level entitlement. Its JSON: <see cref="EntitlementValueJsonConverter"/>.
/// </summary>
[JsonConverter(typeof(EntitlementValueJsonConverter))]
public sealed record EntitlementValue
{
    private EntitlementValue(EntitlementType type, bool enabled, int? limit, string? levelCode)
    {
        Type = type;
        Enabled = enabled;
        Limit = limit;
        LevelCode = levelCode;
    }

    public EntitlementType Type { get; }

    /// <summary>A boolean's value; false for other types.</summary>
    public bool Enabled { get; }

    /// <summary>A quota's limit; null when it is unlimited, and for other types.</summary>
    public int? Limit { get; }

    /// <summary>A level entitlement's level; null when it has none, and for other types.</summary>
    public string? LevelCode { get; }

    /// <summary>Whether a quota has no limit.</summary>
    public bool Unlimited => Type == EntitlementType.Quota && Limit is null;

    public static EntitlementValue Flag(bool enabled) => new(EntitlementType.Boolean, enabled, null, null);

    /// <summary>A quota of at most <paramref name="limit"/> items; null is unlimited.</summary>
    public static EntitlementValue Quota(int? limit)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A quota can't be negative.");
        }

        return new(EntitlementType.Quota, false, limit, null);
    }

    public static EntitlementValue UnlimitedQuota { get; } = Quota(null);

    /// <summary>A level entitlement at <paramref name="level"/>; null is no level.</summary>
    public static EntitlementValue Level(string? level)
    {
        if (level is not null && !EntitlementDefinition.IsValidCode(level))
        {
            throw new ArgumentException($"Level '{level}' must be UPPER_SNAKE_CASE.", nameof(level));
        }

        return new(EntitlementType.Level, false, null, level);
    }

    /// <summary>As the catalogue and authorization.md write it: <c>yes</c>/<c>no</c>, a number or <c>unlimited</c>, a level or <c>none</c>.</summary>
    public override string ToString() => Type switch
    {
        EntitlementType.Boolean => Enabled ? "yes" : "no",
        EntitlementType.Quota => Limit?.ToString(CultureInfo.InvariantCulture) ?? "unlimited",
        EntitlementType.Level => LevelCode ?? "none",
        _ => Type.ToString(),
    };
}
