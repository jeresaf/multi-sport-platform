using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports;

public interface ISportRepository
{
    Task<Sport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sport>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sport>> GetActiveSportsAsync(CancellationToken cancellationToken = default);
    Task<SportCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SportCategory>> GetCategoriesBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetVersionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetPublishedVersionAsync(Guid sportId, CancellationToken cancellationToken = default);
    void Add(Sport sport);
    void Update(Sport sport);
    void AddCategory(SportCategory category);
    void UpdateCategory(SportCategory category);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}