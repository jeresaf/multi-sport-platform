using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.Contests;

public interface IContestRepository
{
    Task<Contest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetByRoundIdAsync(Guid roundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetByStatusAsync(ContestStatus status, CancellationToken cancellationToken = default);
    Task<ContestParticipant?> GetParticipantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestParticipant>> GetParticipantsByContestIdAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<ContestEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestEntry>> GetEntriesByContestIdAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<ContestPrize?> GetPrizeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestPrize>> GetPrizesByContestIdAsync(Guid contestId, CancellationToken cancellationToken = default);
    void Add(Contest contest);
    void Update(Contest contest);
    void AddEntry(ContestEntry entry);
    void UpdateEntry(ContestEntry entry);
    void AddPrize(ContestPrize prize);
    void UpdatePrize(ContestPrize prize);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}