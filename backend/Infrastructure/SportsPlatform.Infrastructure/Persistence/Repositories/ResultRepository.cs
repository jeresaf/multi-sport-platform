using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Results;
using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Infrastructure.Persistence.Repositories;

public sealed class ResultRepository : IResultRepository
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<ContestResult> _results;

    public ResultRepository(SportsPlatformDbContext context)
    {
        _context = context;
        _results = context.ContestResults;
    }

    public Task<ContestResult?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _results.FirstOrDefaultAsync(result => result.Id == id, cancellationToken);

    public Task<ContestResult?> GetByContestAndEventAndParticipantAsync(
        Guid contestId,
        Guid eventId,
        Guid participantId,
        CancellationToken cancellationToken = default) =>
        _results.FirstOrDefaultAsync(
            result =>
                result.ContestId == contestId &&
                result.EventId == eventId &&
                result.ParticipantId == participantId,
            cancellationToken);

    public async Task<IReadOnlyList<ContestResult>> GetByContestIdAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        await _results
            .Where(result => result.ContestId == contestId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContestResult>> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        await _results
            .Where(result => result.EventId == eventId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContestResult>> GetByParticipantIdAsync(
        Guid participantId,
        CancellationToken cancellationToken = default) =>
        await _results
            .Where(result => result.ParticipantId == participantId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContestResult>> GetByStatusAsync(
        ContestResultStatus status,
        CancellationToken cancellationToken = default) =>
        await _results
            .Where(result => result.Status == status)
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<ContestResult>> GetPendingResultsAsync(
        CancellationToken cancellationToken = default) =>
        GetByStatusAsync(ContestResultStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<ContestResult>> GetOfficialResultsAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        await _results
            .Where(result =>
                result.ContestId == contestId &&
                (result.Status == ContestResultStatus.Official ||
                 result.Status == ContestResultStatus.Published))
            .ToListAsync(cancellationToken);

    public void Add(ContestResult result) => _results.Add(result);

    public void Update(ContestResult result) => _results.Update(result);

    public async Task<bool> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken) > 0;
}
