using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Domains;

/// <summary>
/// Gives every tenant that has no domain its platform subdomain, <c>&lt;slug&gt;.&lt;Platform:BaseDomain&gt;</c>. New
/// tenants get one when they are created; this covers tenants created before S02-T3b. A migration can't do it because
/// the base domain is configuration. Idempotent: a second run finds nothing to do.
/// </summary>
internal sealed class BackfillPlatformSubdomains(OwnerContextFactory ownerContexts, IOptions<PlatformDomainOptions> options)
    : IPostMigrationStep
{
    public string Name => "tenancy.platform-subdomains";

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await using var context = ownerContexts.Create<TenancyDbContext>();
        var missing = await context.Set<Tenant>().AsNoTracking()
            .Where(t => !context.Set<TenantDomain>().Any(d => d.TenantId == t.Id))
            .Select(t => new { t.Id, t.Slug })
            .ToListAsync(cancellationToken);

        foreach (var tenant in missing)
        {
            context.Add(TenantDomain.PlatformSubdomain(tenant.Id, tenant.Slug, options.Value.BaseDomain));
        }

        await context.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
