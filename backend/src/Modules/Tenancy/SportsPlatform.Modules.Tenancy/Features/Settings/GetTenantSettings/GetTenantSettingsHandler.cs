using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.GetTenantSettings;

/// <summary>
/// The request's tenant's settings. The Host has set the tenant from <c>X-Tenant-Id</c> (membership checked), so the
/// query filter and RLS show that tenant's row only; every tenant has exactly one.
/// </summary>
public sealed class GetTenantSettingsHandler(TenancyDbContext context, AuditScope audit)
{
    public async Task<TenantSettingsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var settings = await context.Set<TenantSettings>().SingleAsync(cancellationToken);
        audit.Describe(TenantSettingsAudit.EntityType, settings.Id);
        return TenantSettingsResponse.From(context.Entry(settings));
    }
}
