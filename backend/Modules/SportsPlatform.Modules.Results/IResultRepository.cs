using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Modules.Results;

public interface IResultRepository
{
    Task<ContestResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContestResult?> GetByContestAndEventAndParticipantAsync(Guid contestId, Guid eventId, Guid participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetByContestIdAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetByStatusAsync(ContestResultStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetPendingResultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestResult>> GetOfficialResultsAsync(Guid contestId, CancellationToken cancellationToken = default);
    void Add(ContestResult result);
    void Update(ContestResult result);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}