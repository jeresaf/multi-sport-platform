using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface IContestService
{
    Task<Contest> CreateContestAsync(Guid tenantId, Guid sportId, string contestShape, CancellationToken cancellationToken = default);
    Task<Contest?> GetContestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetContestsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetContestsByStageAsync(Guid stageId, CancellationToken cancellationToken = default);
    Task<ContestParticipant> AddParticipantAsync(Guid contestId, Guid participantId, string participantType, CancellationToken cancellationToken = default);
    Task<ContestEntry> AddEntryAsync(Guid contestId, Guid participantId, string participantType, CancellationToken cancellationToken = default);
    Task<ContestPrize> AddPrizeAsync(Guid contestId, string name, int position, CancellationToken cancellationToken = default);
    Task ScheduleContestAsync(Guid contestId, string venue, DateTime startTime, CancellationToken cancellationToken = default);
    Task StartContestAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task CompleteContestAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task CancelContestAsync(Guid contestId, CancellationToken cancellationToken = default);
}

public class ContestService : IContestService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Contest> _contests;
    private readonly DbSet<ContestEntry> _entries;
    private readonly DbSet<ContestPrize> _prizes;

    public ContestService(SportsPlatformDbContext context)
    {
        _context = context;
        _contests = context.Set<Contest>();
        _entries = context.Set<ContestEntry>();
        _prizes = context.Set<ContestPrize>();
    }

    public async Task<Contest> CreateContestAsync(Guid tenantId, Guid sportId, string contestShape, CancellationToken cancellationToken = default)
    {
        var contest = new Contest(Guid.NewGuid(), tenantId, sportId, contestShape);
        _contests.Add(contest);
        await _context.SaveChangesAsync(cancellationToken);
        return contest;
    }

    public async Task<Contest?> GetContestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _contests
            .Include(c => c.Participants)
            .Include(c => c.Segments)
                .ThenInclude(segment => segment.NestedSegments)
            .Include(c => c.Events)
            .Include(c => c.Officials)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Contest>> GetContestsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _contests
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contest>> GetContestsByStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        return await _contests
            .Where(c => c.StageId == stageId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContestParticipant> AddParticipantAsync(Guid contestId, Guid participantId, string participantType, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        var participant = contest.AddParticipant(participantId, participantType);
        _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
        return participant;
    }

    public async Task<ContestEntry> AddEntryAsync(Guid contestId, Guid participantId, string participantType, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        var entry = new ContestEntry(Guid.NewGuid(), contestId, contest.TenantId, participantId, participantType);
        _entries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<ContestPrize> AddPrizeAsync(Guid contestId, string name, int position, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        var prize = new ContestPrize(Guid.NewGuid(), contestId, name, position);
        _prizes.Add(prize);
        await _context.SaveChangesAsync(cancellationToken);
        return prize;
    }

    public async Task ScheduleContestAsync(Guid contestId, string venue, DateTime startTime, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        contest.Schedule(venue, startTime);
        _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StartContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        contest.Start();
        _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        contest.Complete();
        _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest == null)
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");

        contest.Cancel();
        _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
