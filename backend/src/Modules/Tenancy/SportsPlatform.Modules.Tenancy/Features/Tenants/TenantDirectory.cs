using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Access;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// Tenant lookups for the Host's tenant resolution. <c>tenants</c> and <c>tenant_domains</c> are global (no RLS), so
/// these reads may come before the scope's tenant is set (S02 D1); the public-site setting and the access (settings and
/// subscription periods) need the tenant.
/// </summary>
internal sealed class TenantDirectory(
    TenancyDbContext context,
    TenantHostCache hostCache,
    ITenantContext tenantContext,
    ITenantSetupAccess setupAccess,
    TimeProvider timeProvider)
    : ITenantDirectory, ITenantAccess
{
    public async Task<Result<TenantSummary>> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Found(await FindAsync(t => t.Id == tenantId, cancellationToken));

    public async Task<IReadOnlyList<TenantSummary>> GetManyAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        if (tenantIds.Count == 0)
        {
            return [];
        }

        return await context.Set<Tenant>().AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new TenantSummary(t.Id, t.Slug, t.Status, t.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<TenantSummary>> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Found(await FindAsync(t => t.Slug == slug, cancellationToken));

    public async Task<Result<TenantSummary>> GetByHostAsync(string host, CancellationToken cancellationToken) =>
        Found(TenantHost.Normalize(host) is { } normalized
            ? await hostCache.GetOrLoadAsync(normalized, () => FindByNormalizedHostAsync(normalized, cancellationToken))
            : null);

    public async Task<TenantAccess?> GetAccessAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await FindAsync(t => t.Id == tenantId, cancellationToken) is { } tenant ? await AccessOfAsync(tenant, cancellationToken) : null;

    public async Task<Result> CheckAdminAccessAsync(Guid tenantId, ActionDefinition action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (await FindAsync(t => t.Id == tenantId, cancellationToken) is not { } tenant)
        {
            return Result.Failure(TenancyErrors.NotFound);
        }

        return tenant.Status switch
        {
            // FULL only for those who set the tenant up (S03 D11); the rest wait until it is activated.
            TenantStatus.Provisioning => await setupAccess.CanSetUpAsync(tenantId, cancellationToken)
                ? Result.Success()
                : Result.Failure(TenancyErrors.Provisioning),

            // The owner can still see why (tenant.access.read); everything else waits for a restore.
            TenantStatus.Suspended or TenantStatus.Archived =>
                action.AvailableWhenBlocked ? Result.Success() : Result.Failure(TenancyErrors.Blocked),

            // Only an active tenant's subscription decides (S04 D8): reads go on while it is READ_ONLY (and skip the
            // periods), writes wait for a period in effect.
            TenantStatus.Active => action.AvailableWhenReadOnly
                || (await AccessOfAsync(tenant, cancellationToken)).Mode == TenantAccessMode.Full
                ? Result.Success()
                : Result.Failure(TenancyErrors.ReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(tenantId)),
        };
    }

    public async Task<Result> CheckPublicAccessAsync(TenantSummary tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenantContext.TenantId != tenant.Id)
        {
            throw new InvalidOperationException("Set the scope's tenant to this tenant before checking its public access.");
        }

        // The status is global; the setting is tenant-owned and read under the tenant (RLS).
        return tenant.Status == TenantStatus.Active
            && await hostCache.GetOrLoadPublicEnabledAsync(tenant.Id, () => context.Set<TenantSettings>().AsNoTracking()
                .Where(s => s.TenantId == tenant.Id)
                .Select(s => s.PublicEnabled)
                .SingleAsync(cancellationToken))
            ? Result.Success()
            : Result.Failure(TenancyErrors.Unavailable);
    }

    /// <summary>
    /// The tenant's access today (S04 D8): the current period on the tenant's date, from its time zone and the app's clock.
    /// Settings and periods are tenant-owned, so the scope's tenant must be this one. Read without tracking: the request's
    /// handler shares this context. Not cached: the dates decide, so a renewal counts from the next request.
    /// </summary>
    private async Task<TenantAccess> AccessOfAsync(TenantSummary tenant, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId != tenant.Id)
        {
            throw new InvalidOperationException("Set the scope's tenant to this tenant before reading its access.");
        }

        var timeZone = await context.Set<TenantSettings>().AsNoTracking()
            .Where(s => s.TenantId == tenant.Id)
            .Select(s => s.TimeZone)
            .SingleAsync(cancellationToken);
        var periods = await context.Set<SubscriptionPeriod>().AsNoTracking()
            .Where(p => p.TenantId == tenant.Id)
            .ToListAsync(cancellationToken);
        var today = TenantCalendar.Today(timeProvider, timeZone);
        var subscription = new TenantSubscription(periods).CurrentOn(today) is { } current
            ? new SubscriptionAccess(current.StatusOn(today), current.StartsOn, current.EndsOn, current.GraceEndsOn)
            : null;
        return new TenantAccess(tenant.Status, TenantAccessModes.Of(tenant.Status, subscription?.Status), timeZone, subscription);
    }

    private static Result<TenantSummary> Found(TenantSummary? tenant) =>
        tenant is not null ? Result.Success(tenant) : Result.Failure<TenantSummary>(TenancyErrors.NotFound);

    private Task<TenantSummary?> FindByNormalizedHostAsync(string host, CancellationToken cancellationToken) =>
        context.Set<TenantDomain>().AsNoTracking()
            .Where(d => d.Host == host && d.Status == TenantDomainStatus.Active)
            .Join(context.Set<Tenant>(), d => d.TenantId, t => t.Id, (d, t) => new TenantSummary(t.Id, t.Slug, t.Status, t.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<TenantSummary?> FindAsync(
        Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<Tenant>().AsNoTracking()
            .Where(predicate)
            .Select(t => new TenantSummary(t.Id, t.Slug, t.Status, t.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
}
