using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Branding.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface IBrandingService
{
    Task<TenantBranding> CreateOrUpdateBrandingAsync(Guid tenantId, string tenantName, string? shortName, string? logoUrl, string? primaryColor, string? secondaryColor, string? accentColor, string? publicSlug, string? contactEmail, string? contactPhone, string? website, CancellationToken cancellationToken = default);
    Task<TenantBranding?> GetBrandingByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public class BrandingService : IBrandingService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<TenantBranding> _brandings;

    public BrandingService(SportsPlatformDbContext context)
    {
        _context = context;
        _brandings = context.Set<TenantBranding>();
    }

    public async Task<TenantBranding> CreateOrUpdateBrandingAsync(Guid tenantId, string tenantName, string? shortName, string? logoUrl, string? primaryColor, string? secondaryColor, string? accentColor, string? publicSlug, string? contactEmail, string? contactPhone, string? website, CancellationToken cancellationToken = default)
    {
        if (!await _context.Tenants.AnyAsync(item => item.Id == tenantId, cancellationToken))
            throw new KeyNotFoundException($"Tenant with ID {tenantId} was not found.");
        var branding = await _brandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
        if (branding == null)
        {
            branding = new TenantBranding(Guid.NewGuid(), tenantId, tenantName);
            _brandings.Add(branding);
        }
        branding.UpdateBranding(tenantName, shortName, logoUrl, primaryColor, secondaryColor, accentColor);
        branding.UpdatePublicInfo(publicSlug, contactEmail, contactPhone, website);
        await _context.SaveChangesAsync(cancellationToken);
        return branding;
    }

    public async Task<TenantBranding?> GetBrandingByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _brandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
    }
}
