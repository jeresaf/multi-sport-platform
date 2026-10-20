using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// Tenant lookups for the Host's tenant resolution. <c>tenants</c> and <c>tenant_domains</c> are global (no RLS), so
/// these reads may come before the scope's tenant is set (S02 D1); only the public-site setting needs the tenant.
/// </summary>
internal sealed class TenantDirectory(
    TenancyDbContext context, TenantHostCache hostCache, ITenantContext tenantContext, ITenantSetupAccess setupAccess)
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

    public async Task<TenantAccessMode?> GetAccessModeAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await FindAsync(t => t.Id == tenantId, cancellationToken) is { } tenant ? AccessModeOf(tenant.Status) : null;

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
            _ => AccessModeOf(tenant.Status) switch
            {
                TenantAccessMode.Full => Result.Success(),

                // The owner can still see why (tenant.access.read); everything else waits for a restore.
                TenantAccessMode.Blocked => action.AvailableWhenBlocked ? Result.Success() : Result.Failure(TenancyErrors.Blocked),

                // Not produced before subscriptions (S04), which add TENANT_READ_ONLY for writes and let reads through.
                TenantAccessMode.ReadOnly => throw new NotSupportedException("READ_ONLY access arrives with subscriptions (S04)."),
                _ => throw new ArgumentOutOfRangeException(nameof(tenantId)),
            },
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
    /// Status-only until subscriptions arrive (S02 D9, state-machines.md "Access mode"). <c>PROVISIONING</c> is
    /// <c>FULL</c> for those who may set the tenant up; step 1 refuses everyone else.
    /// </summary>
    public static TenantAccessMode AccessModeOf(TenantStatus status) => status switch
    {
        TenantStatus.Provisioning or TenantStatus.Active => TenantAccessMode.Full,
        TenantStatus.Suspended or TenantStatus.Archived => TenantAccessMode.Blocked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

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
