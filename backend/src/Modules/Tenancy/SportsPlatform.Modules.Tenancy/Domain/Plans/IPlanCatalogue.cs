namespace SportsPlatform.Modules.Tenancy.Domain.Plans;

/// <summary>
/// The plans as code declares them (<see cref="TenancyPlans"/>): what the Host checks at startup and
/// <c>tenancy.configuration.plans</c> seeds. A service, so tests can declare other plans (S04 D20's test entitlement).
/// </summary>
public interface IPlanCatalogue
{
    /// <summary>In rank order.</summary>
    IReadOnlyList<PlanDefinition> All { get; }

    PlanDefinition? Find(string code);
}

public sealed class PlanCatalogue : IPlanCatalogue
{
    public PlanCatalogue(IEnumerable<PlanDefinition> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        All = [.. plans.OrderBy(plan => plan.Rank)];
    }

    public IReadOnlyList<PlanDefinition> All { get; }

    public PlanDefinition? Find(string code) => All.FirstOrDefault(plan => string.Equals(plan.Code, code, StringComparison.Ordinal));
}
