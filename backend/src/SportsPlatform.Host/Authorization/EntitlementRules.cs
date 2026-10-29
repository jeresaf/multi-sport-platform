using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// Startup check of the entitlements (S04 D1): an action names only a declared entitlement, and only a tenant action
/// names one (step 2 runs for tenant actions only, D10); plan codes and ranks are unique (ranks order upgrades, D4);
/// every plan gives every declared entitlement a value of its type, and nothing else; each module's usage counter
/// (<see cref="QuotaUsageRegistration"/>, D15) counts a declared quota, and no quota has two.
/// </summary>
public static class EntitlementRules
{
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<ActionCatalogueEntry> actions, IEntitlementCatalogue entitlements, IReadOnlyList<PlanDefinition> plans)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(entitlements);
        ArgumentNullException.ThrowIfNull(plans);

        var violations = new List<string>();
        foreach (var (action, _) in actions.Where(entry => entry.Action.Entitlement is not null))
        {
            if (action.Scope != ActionScope.Tenant)
            {
                violations.Add($"{action.Scope} action '{action.Code}' names entitlement {action.Entitlement}; only tenant actions can.");
            }
            else if (entitlements.Find(action.Entitlement!) is null)
            {
                violations.Add($"Action '{action.Code}' names entitlement {action.Entitlement}, which is not declared in a module's *Entitlements class.");
            }
        }

        violations.AddRange(plans.GroupBy(plan => plan.Code, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => $"Plan {group.Key} is defined more than once."));
        violations.AddRange(plans.GroupBy(plan => plan.Rank).Where(group => group.Count() > 1)
            .Select(group => $"Plans {string.Join(", ", group.Select(plan => plan.Code))} share rank {group.Key}."));

        foreach (var plan in plans)
        {
            foreach (var entitlement in entitlements.All)
            {
                if (!plan.Values.TryGetValue(entitlement.Code, out var value))
                {
                    violations.Add($"Plan {plan.Code} gives entitlement {entitlement.Code} no value.");
                }
                else if (!entitlement.Accepts(value))
                {
                    violations.Add($"Plan {plan.Code} gives entitlement {entitlement.Code} the value {value}, which doesn't fit its type or levels.");
                }
            }

            violations.AddRange(plan.Values.Keys.Where(code => entitlements.Find(code) is null).Order(StringComparer.Ordinal)
                .Select(code => $"Plan {plan.Code} values {code}, which is not a declared entitlement."));
        }

        return violations;
    }

    /// <summary>The usage counters' entitlement codes, as registered: each a declared quota, at most one per code.</summary>
    public static IReadOnlyList<string> ValidateQuotaUsages(IReadOnlyList<string> usageCodes, IEntitlementCatalogue entitlements)
    {
        ArgumentNullException.ThrowIfNull(usageCodes);
        ArgumentNullException.ThrowIfNull(entitlements);

        var violations = new List<string>();
        foreach (var code in usageCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (entitlements.Find(code) is not { } definition)
            {
                violations.Add($"A usage counter counts {code}, which is not a declared entitlement.");
            }
            else if (definition.Type != EntitlementType.Quota)
            {
                violations.Add($"A usage counter counts {code}, a {definition.Type} entitlement; only quotas are counted.");
            }

            if (usageCodes.Count(usage => usage == code) > 1)
            {
                violations.Add($"{code} has more than one usage counter.");
            }
        }

        return violations;
    }

    /// <summary>Throws when an action or a plan breaks the rules, listing every violation.</summary>
    public static void EnsureEntitlementsAreConsistent(this WebApplication app, IReadOnlyList<ActionCatalogueEntry> actions)
    {
        ArgumentNullException.ThrowIfNull(app);

        var catalogue = app.Services.GetRequiredService<IEntitlementCatalogue>();
        var violations = Validate(actions, catalogue, app.Services.GetRequiredService<IPlanCatalogue>().All).ToList();
        violations.AddRange(ValidateQuotaUsages(
            [.. app.Services.GetServices<QuotaUsageRegistration>().Select(registration => registration.EntitlementCode)], catalogue));

        if (violations.Count > 0)
        {
            throw new InvalidOperationException("Actions, plans and usage counters must name declared entitlements:\n" + string.Join("\n", violations));
        }
    }
}
