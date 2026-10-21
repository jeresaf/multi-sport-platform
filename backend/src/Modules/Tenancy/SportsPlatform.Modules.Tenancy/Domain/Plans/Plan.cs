using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Tenancy.Domain.Plans;

/// <summary>
/// A plan as stored (<c>tenancy.plans</c>, S04 D2): configuration data, platform-global, read-only for the runtime roles
/// and written only by <c>tenancy.configuration.plans</c> from <see cref="PlanDefinition"/>. Never deleted: a plan no
/// longer declared is deactivated, and comes back active if it is declared again.
/// </summary>
public sealed class Plan : Entity
{
    private readonly List<PlanEntitlement> _entitlements = [];
    private string _name = string.Empty;
    private int _rank;
    private bool _isActive;

    public Plan(PlanDefinition definition)
        : base(IdFor(definition?.Code ?? throw new ArgumentNullException(nameof(definition))))
    {
        Code = definition.Code;
        Apply(definition);
    }

    private Plan()
    {
    }

    /// <summary>Stable, e.g. <c>STANDARD</c>; never changes.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get => _name; private set => _name = value; }

    public int Rank { get => _rank; private set => _rank = value; }

    public bool IsActive { get => _isActive; private set => _isActive = value; }

    /// <summary>Its value of each entitlement (load with <c>Include</c>).</summary>
    public IReadOnlyList<PlanEntitlement> Entitlements => _entitlements;

    public static Guid IdFor(string code) => DeterministicIds.Create($"tenancy.plan:{code}");

    /// <summary>
    /// Takes the definition's name, rank and values (the entitlements must be loaded); returns whether anything changed.
    /// A value for an entitlement the definition no longer lists stays as it is: nothing reads it (reads join the
    /// declared entitlements), and seeders never delete.
    /// </summary>
    public bool Apply(PlanDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!string.Equals(definition.Code, Code, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Plan {Code} can't take the values of plan {definition.Code}.", nameof(definition));
        }

        var changed = Set(ref _name, definition.Name) | Set(ref _rank, definition.Rank) | Set(ref _isActive, true);
        foreach (var (code, value) in definition.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (_entitlements.Find(e => string.Equals(e.EntitlementCode, code, StringComparison.Ordinal)) is { } entitlement)
            {
                changed |= entitlement.Set(value);
            }
            else
            {
                _entitlements.Add(new PlanEntitlement(this, code, value));
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Returns whether anything changed.</summary>
    public bool Deactivate() => Set(ref _isActive, false);

    private static bool Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
