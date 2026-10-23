using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;

/// <summary>
/// The request's tenant's status, access mode, time zone and current subscription period, and the change that put it in
/// that status. The Host has set the tenant from
/// <c>X-Tenant-Id</c> (membership checked), so the status history shows that tenant's rows only (RLS).
/// </summary>
public sealed class GetTenantAccessHandler(
    TenancyDbContext context, ITenantContext tenantContext, ITenantAccess tenantAccess, AuditScope audit)
{
    public async Task<TenantAccessResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant actions run under the request's tenant.");
        audit.Describe(TenantAudit.EntityType, tenantId);

        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("The Host resolved the request's tenant, so it exists.");
        var status = access.Status;

        // The change that put the tenant in its status, and why; every status was entered by a recorded change (the
        // creation row for PROVISIONING).
        var last = await context.Set<TenantStatusChange>().AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ToStatus == status)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id) // a stable answer for changes at one instant
            .Select(c => new { c.Reason, c.OccurredAt })
            .FirstAsync(cancellationToken);

        return new TenantAccessResponse(status, access.Mode, last.Reason, last.OccurredAt, access.TimeZone, access.Subscription);
    }
}
