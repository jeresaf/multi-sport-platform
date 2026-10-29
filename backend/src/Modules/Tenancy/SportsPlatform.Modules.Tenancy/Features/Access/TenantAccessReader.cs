using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Access;

/// <summary>
/// Reads a tenant's access inputs and the plans' values through <see cref="TenantAccessCache"/> (S04 D7), for step 1
/// and the effective entitlements. Settings, periods and overrides are tenant-owned, so the scope's tenant must be the
/// one read. Read without tracking: the request's handler shares this context.
/// </summary>
public sealed class TenantAccessReader(
    TenancyDbContext context, TenantAccessCache cache, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public Task<TenantAccessInputs> InputsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId != tenantId)
        {
            throw new InvalidOperationException("Set the scope's tenant to this tenant before reading its access.");
        }

        return cache.GetOrLoadAsync(tenantId, async () => new TenantAccessInputs(
            await context.Set<TenantSettings>().AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.TimeZone)
                .SingleAsync(cancellationToken),
            await context.Set<SubscriptionPeriod>().AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync(cancellationToken),
            await context.Set<EntitlementOverride>().AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.RemovedAt == null)
                .ToListAsync(cancellationToken)));
    }

    public Task<PlanValues> PlanValuesAsync(CancellationToken cancellationToken) =>
        cache.GetOrLoadPlanValuesAsync(async () =>
        {
            var rows = await context.Set<PlanEntitlement>().AsNoTracking()
                .Join(context.Set<Plan>(), e => e.PlanId, p => p.Id, (e, p) => new { PlanCode = p.Code, e.EntitlementCode, e.Value })
                .ToListAsync(cancellationToken);
            var active = await context.Set<Plan>().AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Rank)
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
            return new PlanValues(
                rows
                    .GroupBy(row => row.PlanCode, StringComparer.Ordinal)
                    .ToDictionary(
                        plan => plan.Key,
                        plan => (IReadOnlyDictionary<string, EntitlementValue>)plan.ToDictionary(row => row.EntitlementCode, row => row.Value, StringComparer.Ordinal),
                        StringComparer.Ordinal),
                active);
        });

    /// <summary>The tenant's date (<see cref="TenantCalendar"/>), never cached.</summary>
    public DateOnly TodayIn(string timeZone) => TenantCalendar.Today(timeProvider, timeZone);
}
