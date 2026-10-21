using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Plans.ListPlans;

public sealed record ListPlansResponse(IReadOnlyList<PlanResponse> Items);

public sealed record PlanResponse(string Code, string Name, int Rank, IReadOnlyList<PlanEntitlementResponse> Entitlements);

/// <summary>
/// An entitlement's definition and its <paramref name="Value"/> on the plan: null when the stored plan has no value for
/// it yet (the code declares an entitlement that <c>seed --category CONFIGURATION</c> hasn't written).
/// </summary>
public sealed record PlanEntitlementResponse(
    string Code,
    string Name,
    string Description,
    EntitlementType Type,
    string? Unit,
    IReadOnlyList<string> Levels,
    EntitlementValue Default,
    EntitlementValue? Value,
    string EnforcedBy);

/// <summary>
/// The active plans as seeded, in rank order, each with the declared entitlements in code order (ordinal): the stored
/// values joined with the catalogue's definitions. A stored value for an entitlement no longer declared is left out.
/// Saves nothing, so the Host writes
/// this read's audit entry after the response; it names no tenant.
/// </summary>
public sealed class ListPlansHandler(TenancyDbContext context, IEntitlementCatalogue entitlements)
{
    public async Task<ListPlansResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var plans = await context.Set<Plan>().AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Entitlements)
            .ToListAsync(cancellationToken);

        var definitions = entitlements.All.OrderBy(e => e.Code, StringComparer.Ordinal).ToList();
        var items = plans
            .OrderBy(p => p.Rank).ThenBy(p => p.Code, StringComparer.Ordinal)
            .Select(plan =>
            {
                var values = plan.Entitlements.ToDictionary(e => e.EntitlementCode, e => e.Value, StringComparer.Ordinal);
                return new PlanResponse(plan.Code, plan.Name, plan.Rank, [.. definitions.Select(definition => new PlanEntitlementResponse(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Type,
                    definition.Unit,
                    definition.Levels,
                    definition.Default,
                    values.GetValueOrDefault(definition.Code),
                    definition.EnforcedBy))]);
            })
            .ToList();
        return new ListPlansResponse(items);
    }
}
