using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Site.GetSite;

/// <summary>The site of the request's public tenant, which the Host has already resolved and checked (S02 D6).</summary>
public sealed class GetSiteHandler(TenancyDbContext context, ITenantContext tenantContext)
{
    public async Task<SiteResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("The site endpoint runs only under a resolved public tenant.");

        var site = await context.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Slug,
                t.DisplayName,
                PrimaryDomain = context.Set<TenantDomain>()
                    .Where(d => d.TenantId == t.Id && d.IsPrimary && d.Status == TenantDomainStatus.Active)
                    .Select(d => d.Host)
                    .FirstOrDefault(),
            })
            .SingleAsync(cancellationToken);

        // The request resolved through an ACTIVE domain, and every tenant's platform subdomain is its primary one.
        return new SiteResponse(
            site.Slug,
            site.DisplayName,
            site.PrimaryDomain ?? throw new InvalidOperationException($"Tenant {tenantId} has no active primary domain."));
    }
}
