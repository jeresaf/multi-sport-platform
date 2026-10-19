using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// Tenant lookups for the Host's tenant resolution. <c>tenants</c> is global (no RLS), so these reads may come before
/// the scope's tenant is set (S02 D1).
/// </summary>
internal sealed class TenantDirectory(TenancyDbContext context, TenantHostCache hostCache) : ITenantDirectory, ITenantAccess
{
    public async Task<Result<TenantSummary>> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await FindAsync(t => t.Id == tenantId, cancellationToken) is { } tenant
            ? Result.Success(tenant)
            : Result.Failure<TenantSummary>(TenancyErrors.NotFound);

    public Task<TenantSummary?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        FindAsync(t => t.Slug == slug, cancellationToken);

    public Task<TenantSummary?> FindByHostAsync(string host, CancellationToken cancellationToken) =>
        TenantHost.Normalize(host) is { } normalized
            ? hostCache.GetOrLoadAsync(normalized, () => FindByNormalizedHostAsync(normalized, cancellationToken))
            : Task.FromResult<TenantSummary?>(null);

    public async Task<TenantAccessMode?> GetAccessModeAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await FindAsync(t => t.Id == tenantId, cancellationToken) is { } tenant ? AccessModeOf(tenant.Status) : null;

    public async Task<Result> CheckAdminAccessAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await GetAccessModeAsync(tenantId, cancellationToken) switch
        {
            TenantAccessMode.Full => Result.Success(),
            TenantAccessMode.Blocked => Result.Failure(TenancyErrors.Blocked),
            // Not produced before subscriptions (S04), which add TENANT_READ_ONLY for writes and let reads through.
            TenantAccessMode.ReadOnly => throw new NotSupportedException("READ_ONLY access arrives with subscriptions (S04)."),
            null => Result.Failure(TenancyErrors.NotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(tenantId)),
        };

    public Result CheckPublicAccess(TenantSummary tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return tenant is { Status: TenantStatus.Active, PublicEnabled: true }
            ? Result.Success()
            : Result.Failure(TenancyErrors.Unavailable);
    }

    /// <summary>Status-only until subscriptions arrive (S02 D9, state-machines.md "Access mode").</summary>
    private static TenantAccessMode AccessModeOf(TenantStatus status) => status switch
    {
        TenantStatus.Provisioning or TenantStatus.Active => TenantAccessMode.Full,
        TenantStatus.Suspended or TenantStatus.Archived => TenantAccessMode.Blocked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private Task<TenantSummary?> FindByNormalizedHostAsync(string host, CancellationToken cancellationToken) =>
        context.Set<TenantDomain>().AsNoTracking()
            .Where(d => d.Host == host && d.Status == TenantDomainStatus.Active)
            .Join(context.Set<Tenant>(), d => d.TenantId, t => t.Id, (d, t) => new TenantSummary(t.Id, t.Slug, t.Status, t.PublicEnabled))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<TenantSummary?> FindAsync(
        Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<Tenant>().AsNoTracking()
            .Where(predicate)
            .Select(t => new TenantSummary(t.Id, t.Slug, t.Status, t.PublicEnabled))
            .SingleOrDefaultAsync(cancellationToken);
}
