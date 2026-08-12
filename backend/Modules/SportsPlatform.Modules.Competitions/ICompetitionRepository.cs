using SportsPlatform.Modules.Competitions.Entities;

namespace SportsPlatform.Modules.Competitions;

public interface ICompetitionRepository
{
    Task<Competition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Competition>> GetBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Competition>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Competition>> GetByStatusAsync(CompetitionStatus status, CancellationToken cancellationToken = default);
    Task<CompetitionFormat?> GetFormatByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionFormat>> GetFormatsBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<Edition?> GetEditionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Edition>> GetEditionsByCompetitionIdAsync(Guid competitionId, CancellationToken cancellationToken = default);
    Task<Stage?> GetStageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Group?> GetGroupByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Round?> GetRoundByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Competition competition);
    void Update(Competition competition);
    void AddFormat(CompetitionFormat format);
    void UpdateFormat(CompetitionFormat format);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}