using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>"Today" for the scope's tenant: the date in its time zone (S06 D13), which decides who is a minor.</summary>
public sealed class TenantToday(ITenantAccess access, ITenantContext tenant, TimeProvider timeProvider)
{
    public async Task<DateOnly> GetAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("The tenant's date needs a tenant.");
        var tenantAccess = await access.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is unknown.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tenantAccess.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }
}
