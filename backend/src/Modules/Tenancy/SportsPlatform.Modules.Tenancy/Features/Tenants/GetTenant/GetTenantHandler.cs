using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.GetTenant;

/// <summary>One tenant. The Host runs it under the route's tenant (S02 D2), so this read's audit entry names it.</summary>
public sealed class GetTenantHandler(TenancyDbContext context, AuditScope audit)
{
    public async Task<Result<TenantResponse>> HandleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        audit.Describe(TenantAudit.EntityType, tenantId);

        var tenant = await context.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(TenantResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? TenancyErrors.NotFound : Result.Success(tenant);
    }
}
