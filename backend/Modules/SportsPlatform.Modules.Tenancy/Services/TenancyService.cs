using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Modules.Tenancy.Services;

public interface ITenancyService
{
    Task<Tenant> CreateTenantAsync(string name, string shortName, string slug, CancellationToken cancellationToken = default);
    Task<Tenant?> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task UpdateTenantAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task SuspendTenantAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateTenantAsync(Guid id, CancellationToken cancellationToken = default);
    Task ArchiveTenantAsync(Guid id, CancellationToken cancellationToken = default);

    // Contact management
    Task<IReadOnlyList<TenantContact>> GetContactsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantContact> CreateContactAsync(Guid tenantId, string name, string email, string? phone, string? role, bool isPrimary, CancellationToken cancellationToken = default);
    Task<TenantContact> UpdateContactAsync(Guid contactId, string name, string email, string? phone, string? role, bool isPrimary, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default);

    // Localization
    Task UpdateLocalizationAsync(Guid tenantId, string? country, string? timezone, string? currency, string? defaultLanguage, CancellationToken cancellationToken = default);
}

public class TenancyService : ITenancyService
{
    private readonly ITenantRepository _tenantRepository;

    public TenancyService(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<Tenant> CreateTenantAsync(string name, string shortName, string slug, CancellationToken cancellationToken = default)
    {
        var tenant = new Tenant(Guid.NewGuid(), name, shortName, slug);
        if (await _tenantRepository.GetBySlugAsync(tenant.Slug, cancellationToken) is not null)
            throw new InvalidOperationException($"A tenant with slug '{tenant.Slug}' already exists.");
        return await _tenantRepository.AddAsync(tenant, cancellationToken);
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetBySlugAsync(slug, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetAllAsync(cancellationToken);
    }

    public async Task UpdateTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    public async Task SuspendTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null) throw new KeyNotFoundException($"Tenant with ID {id} was not found.");
        tenant.Suspend();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    public async Task ActivateTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null) throw new KeyNotFoundException($"Tenant with ID {id} was not found.");
        tenant.Activate();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    public async Task ArchiveTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null) throw new KeyNotFoundException($"Tenant with ID {id} was not found.");
        tenant.Archive();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    // Contact management
    public async Task<IReadOnlyList<TenantContact>> GetContactsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetContactsByTenantAsync(tenantId, cancellationToken);
    }

    public async Task<TenantContact> CreateContactAsync(Guid tenantId, string name, string email, string? phone, string? role, bool isPrimary, CancellationToken cancellationToken = default)
    {
        if (await _tenantRepository.GetByIdAsync(tenantId, cancellationToken) is null)
            throw new KeyNotFoundException($"Tenant with ID {tenantId} was not found.");
        await ClearOtherPrimaryContactsAsync(tenantId, null, isPrimary, cancellationToken);
        var contact = new TenantContact(Guid.NewGuid(), tenantId, name, email, phone, role, isPrimary);
        return await _tenantRepository.AddContactAsync(contact, cancellationToken);
    }

    public async Task<TenantContact> UpdateContactAsync(Guid contactId, string name, string email, string? phone, string? role, bool isPrimary, CancellationToken cancellationToken = default)
    {
        var contact = await _tenantRepository.GetContactByIdAsync(contactId, cancellationToken);
        if (contact == null)
            throw new KeyNotFoundException($"Contact with ID {contactId} was not found.");

        await ClearOtherPrimaryContactsAsync(contact.TenantId, contact.Id, isPrimary, cancellationToken);
        contact.UpdateDetails(name, email, phone, role, isPrimary);
        await _tenantRepository.UpdateContactAsync(contact, cancellationToken);
        return contact;
    }

    public async Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await _tenantRepository.GetContactByIdAsync(contactId, cancellationToken);
        if (contact == null)
            throw new KeyNotFoundException($"Contact with ID {contactId} was not found.");

        await _tenantRepository.DeleteContactAsync(contact, cancellationToken);
    }

    // Localization
    public async Task UpdateLocalizationAsync(Guid tenantId, string? country, string? timezone, string? currency, string? defaultLanguage, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            throw new KeyNotFoundException($"Tenant with ID {tenantId} was not found.");

        tenant.UpdateLocalization(country, timezone, currency, defaultLanguage);
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    private async Task ClearOtherPrimaryContactsAsync(
        Guid tenantId,
        Guid? exceptContactId,
        bool newContactIsPrimary,
        CancellationToken cancellationToken)
    {
        if (!newContactIsPrimary) return;
        var existing = await _tenantRepository.GetContactsByTenantAsync(tenantId, cancellationToken);
        foreach (var primary in existing.Where(contact => contact.IsPrimary && contact.Id != exceptContactId))
        {
            primary.UpdateDetails(primary.Name, primary.Email, primary.Phone, primary.Role, false);
            await _tenantRepository.UpdateContactAsync(primary, cancellationToken);
        }
    }
}
