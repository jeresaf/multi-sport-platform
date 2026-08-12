using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Modules.Tenancy;

public interface ITenantRepository : IRepository<Tenant>
{
    new Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    new Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    new Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    new Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    new Task DeleteAsync(Tenant tenant, CancellationToken cancellationToken = default);

    // Contact management
    Task<IReadOnlyList<TenantContact>> GetContactsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantContact?> GetContactByIdAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<TenantContact> AddContactAsync(TenantContact contact, CancellationToken cancellationToken = default);
    Task UpdateContactAsync(TenantContact contact, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(TenantContact contact, CancellationToken cancellationToken = default);
}
