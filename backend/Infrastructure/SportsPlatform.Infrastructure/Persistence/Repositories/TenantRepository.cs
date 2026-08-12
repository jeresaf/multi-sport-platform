using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly SportsPlatformDbContext _context;

    public TenantRepository(SportsPlatformDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => new UnitOfWork(_context);

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> FindAsync(
        Expression<Func<Tenant, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(tenant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Contact management
    public async Task<IReadOnlyList<TenantContact>> GetContactsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantContacts
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantContact?> GetContactByIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantContacts.FirstOrDefaultAsync(c => c.Id == contactId, cancellationToken);
    }

    public async Task<TenantContact> AddContactAsync(TenantContact contact, CancellationToken cancellationToken = default)
    {
        await _context.TenantContacts.AddAsync(contact, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return contact;
    }

    public async Task UpdateContactAsync(TenantContact contact, CancellationToken cancellationToken = default)
    {
        _context.TenantContacts.Update(contact);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteContactAsync(TenantContact contact, CancellationToken cancellationToken = default)
    {
        _context.TenantContacts.Remove(contact);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
