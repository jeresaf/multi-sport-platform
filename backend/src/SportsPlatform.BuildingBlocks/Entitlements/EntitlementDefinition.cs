using System.Text.RegularExpressions;

namespace SportsPlatform.BuildingBlocks.Entitlements;

/// <summary>What kind of value an entitlement has (docs/architecture/authorization.md, "Entitlements").</summary>
public enum EntitlementType
{
    /// <summary>On or off, e.g. <c>CUSTOM_DOMAIN</c>.</summary>
    Boolean,

    /// <summary>A limit on how many items a tenant may have, e.g. <c>MAX_TEAMS</c>; unlimited is no limit.</summary>
    Quota,

    /// <summary>One of an ordered list of levels, e.g. <c>REPORTS</c>: <c>BASIC</c> &lt; <c>ADVANCED</c>.</summary>
    Level,
}

/// <summary>
/// A tenant-level commercial capability or limit (S04 D1). Tenancy declares every entitlement as a public static readonly
/// field of a static <c>*Entitlements</c> class; actions name one by its code, and plans give each one a value. The CLI
/// exports them to <c>docs/generated/entitlements.md</c>.
/// </summary>
public sealed partial class EntitlementDefinition
{
    public EntitlementDefinition(
        string code,
        string name,
        string description,
        EntitlementType type,
        string enforcedBy,
        string since,
        string? unit = null,
        IReadOnlyList<string>? levels = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(enforcedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(since);
        if (!IsValidCode(code))
        {
            throw new ArgumentException($"Entitlement code '{code}' must be UPPER_SNAKE_CASE, e.g. MAX_TEAMS.", nameof(code));
        }

        // Only a quota counts something; it says what (sports, teams, GB).
        if ((type == EntitlementType.Quota) != !string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException($"Entitlement {code}: a unit is required for a quota and only for a quota.", nameof(unit));
        }

        levels ??= [];
        if ((type == EntitlementType.Level) != (levels.Count > 0))
        {
            throw new ArgumentException($"Entitlement {code}: levels are required for a level entitlement and only for one.", nameof(levels));
        }

        if (levels.Any(level => !IsValidCode(level)) || levels.Distinct(StringComparer.Ordinal).Count() != levels.Count)
        {
            throw new ArgumentException($"Entitlement {code}: levels must be distinct UPPER_SNAKE_CASE codes.", nameof(levels));
        }

        Code = code;
        Name = name;
        Description = description;
        Type = type;
        EnforcedBy = enforcedBy;
        Since = since;
        Unit = unit;
        Levels = [.. levels];
    }

    /// <summary>Stable, UPPER_SNAKE_CASE, e.g. <c>MAX_STAFF_USERS</c>; what actions and plans name.</summary>
    public string Code { get; }

    /// <summary>Shown on the Plans and Subscription pages, e.g. "Staff users".</summary>
    public string Name { get; }

    public string Description { get; }

    public EntitlementType Type { get; }

    /// <summary>The module that checks it (S04 D1), e.g. "Identity"; text, so modules that don't exist yet need nothing.</summary>
    public string EnforcedBy { get; }

    /// <summary>The slice that introduced the entitlement, e.g. <c>S04</c>.</summary>
    public string Since { get; }

    /// <summary>What a quota counts, e.g. <c>teams</c>; null for other types.</summary>
    public string? Unit { get; }

    /// <summary>A level entitlement's levels, lowest first; empty for other types.</summary>
    public IReadOnlyList<string> Levels { get; }

    /// <summary>The value for a tenant with no subscription period (S04 D2): off, a limit of 0, or no level.</summary>
    public EntitlementValue Default => Type switch
    {
        EntitlementType.Boolean => EntitlementValue.Flag(false),
        EntitlementType.Quota => EntitlementValue.Quota(0),
        EntitlementType.Level => EntitlementValue.Level(null),
        _ => throw new InvalidOperationException($"Unknown entitlement type {Type}."),
    };

    /// <summary>Whether <paramref name="value"/> is of this entitlement's type and, for a level, one of its levels.</summary>
    public bool Accepts(EntitlementValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Type == Type && (value.LevelCode is null || Levels.Contains(value.LevelCode, StringComparer.Ordinal));
    }

    /// <summary>Whether <paramref name="value"/> has the format of an entitlement code.</summary>
    public static bool IsValidCode(string value) => Format().IsMatch(value);

    public override string ToString() => Code;

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$")]
    private static partial Regex Format();
}
