using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;

/// <summary>
/// The request's tenant's status, access mode and the change that put it in that status. The Host has set the tenant from
/// <c>X-Tenant-Id</c> (membership checked), so the status history shows that tenant's rows only (RLS).
/// </summary>
public sealed class GetTenantAccessHandler(TenancyDbContext context, ITenantContext tenantContext, AuditScope audit)
{
    public async Task<TenantAccessResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant actions run under the request's tenant.");
        audit.Describe(TenantAudit.EntityType, tenantId);

        var status = await context.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Status)
            .SingleAsync(cancellationToken);

        // The change that put the tenant in its status, and why; every status was entered by a recorded change (the
        // creation row for PROVISIONING).
        var last = await context.Set<TenantStatusChange>().AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ToStatus == status)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id) // a stable answer for changes at one instant
            .Select(c => new { c.Reason, c.OccurredAt })
            .FirstAsync(cancellationToken);

        return new TenantAccessResponse(status, TenantDirectory.AccessModeOf(status), last.Reason, last.OccurredAt);
    }
}
