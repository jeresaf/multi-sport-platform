using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface IContestsService
{
    Task<Contest> CreateContestAsync(Guid tenantId, Guid sportId, string contestShape, IReadOnlyList<string>? allowedParticipantTypes = null, CancellationToken cancellationToken = default);
    Task<ContestParticipant> AddParticipantAsync(Guid contestId, Guid participantId, string participantType, CancellationToken cancellationToken = default);
    Task<ContestSegment> AddSegmentAsync(
        Guid contestId,
        string name,
        string segmentType,
        int sortOrder,
        bool isOptional = false,
        string? activationCondition = null,
        CancellationToken cancellationToken = default);
    Task<ContestMetric> RecordMetricAsync(Guid contestId, string name, decimal? numericValue, string? textValue, string? unit, Guid? participantId, Guid? segmentId, string dataType = "Decimal", int precision = 6, CancellationToken cancellationToken = default);
    Task<ContestOfficial> AddOfficialAsync(Guid contestId, Guid officialId, string role, string? displayName, CancellationToken cancellationToken = default);
    Task<Contest?> GetContestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contest>> GetContestsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateContestAsync(Contest contest, CancellationToken cancellationToken = default);
    Task StartContestAsync(Guid id, CancellationToken cancellationToken = default);
    Task CompleteContestAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelContestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ActivateSegmentAsync(Guid contestId, Guid segmentId, CancellationToken cancellationToken = default);
}

public class ContestsService : IContestsService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Contest> _contests;

    public ContestsService(SportsPlatformDbContext context)
    {
        _context = context;
        _contests = context.Set<Contest>();
    }

    public async Task<Contest> CreateContestAsync(Guid tenantId, Guid sportId, string contestShape, IReadOnlyList<string>? allowedParticipantTypes = null, CancellationToken cancellationToken = default)
    {
        var contest = new Contest(Guid.NewGuid(), tenantId, sportId, contestShape);
        if (allowedParticipantTypes is { Count: > 0 })
            contest.ConfigureParticipantTypes(allowedParticipantTypes);
        _contests.Add(contest);
        await _context.SaveChangesAsync(cancellationToken);
        return contest;
    }

    public async Task<ContestParticipant> AddParticipantAsync(
        Guid contestId,
        Guid participantId,
        string participantType,
        CancellationToken cancellationToken = default)
    {
        var contest = await GetContestByIdAsync(contestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contest with ID {contestId} was not found.");
        var participant = await _context.Participants
            .FirstOrDefaultAsync(item => item.Id == participantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Participant with ID {participantId} was not found.");
        if (participant.TenantId != contest.TenantId)
            throw new InvalidOperationException("The participant and contest must belong to the same tenant.");

        if (!participant.Type.ToString().Equals(participantType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Participant type must match the participant profile type {participant.Type}.");
        var existing = contest.Participants.FirstOrDefault(item =>
            item.ParticipantId == participantId);
        if (existing is not null) return existing;

        var added = contest.AddParticipant(participantId, participant.Type.ToString());
        // Owned collection entries with generated GUIDs can otherwise be
        // inferred as existing rows when appended to an already persisted
        // aggregate (notably by the EF in-memory provider).
        _context.Entry(added).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return added;
    }

    public async Task<ContestSegment> AddSegmentAsync(
        Guid contestId,
        string name,
        string segmentType,
        int sortOrder,
        bool isOptional = false,
        string? activationCondition = null,
        CancellationToken cancellationToken = default)
    {
        var contest = await GetContestByIdAsync(contestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contest with ID {contestId} was not found.");
        var segment = contest.AddSegment(
            name, segmentType, sortOrder, isOptional, activationCondition);
        // A generated GUID is non-default, so EF can otherwise infer that a
        // newly appended owned collection entry already exists and issue an UPDATE.
        var segmentEntry = _context.Entry(contest)
            .Collection(item => item.Segments)
            .FindEntry(segment)
            ?? throw new InvalidOperationException("The new contest segment was not attached to its owner.");
        segmentEntry.State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return segment;
    }

    public async Task<Contest?> GetContestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _contests
            .AsSplitQuery()
            .Include(contest => contest.Participants)
            .Include(contest => contest.Segments)
                .ThenInclude(segment => segment.NestedSegments)
            .Include(contest => contest.Officials)
            .Include(contest => contest.Events)
            .Include(contest => contest.Metrics)
            .FirstOrDefaultAsync(contest => contest.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Contest>> GetContestsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _contests
            .AsNoTracking()
            .AsSplitQuery()
            .Include(contest => contest.Participants)
            .Include(contest => contest.Segments)
                .ThenInclude(segment => segment.NestedSegments)
            .Include(contest => contest.Officials)
            .Include(contest => contest.Metrics)
            .Where(contest => contest.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContestMetric> RecordMetricAsync(
        Guid contestId, string name, decimal? numericValue, string? textValue,
        string? unit, Guid? participantId, Guid? segmentId,
        string dataType = "Decimal", int precision = 6,
        CancellationToken cancellationToken = default)
    {
        var contest = await GetContestByIdAsync(contestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contest with ID {contestId} was not found.");
        var metric = contest.RecordMetric(
            name, numericValue, textValue, unit, participantId, segmentId,
            dataType, precision);
        _context.Entry(metric).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return metric;
    }

    public async Task<ContestOfficial> AddOfficialAsync(
        Guid contestId, Guid officialId, string role, string? displayName,
        CancellationToken cancellationToken = default)
    {
        var contest = await GetContestByIdAsync(contestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contest with ID {contestId} was not found.");
        if (officialId == Guid.Empty) throw new ArgumentException("An official is required.", nameof(officialId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("An official role is required.", nameof(role));
        var existing = contest.Officials.SingleOrDefault(item => item.OfficialId == officialId
            && item.Role.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        contest.AddOfficial(officialId, role.Trim(), displayName);
        var official = contest.Officials.Last();
        _context.Entry(official).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return official;
    }

    public async Task UpdateContestAsync(Contest contest, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(contest).State == EntityState.Detached)
            _contests.Update(contest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StartContestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { id }, cancellationToken);
        if (contest != null)
        {
            contest.Start();
            _contests.Update(contest);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CompleteContestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { id }, cancellationToken);
        if (contest != null)
        {
            contest.Complete();
            _contests.Update(contest);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CancelContestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contest = await _contests.FindAsync(new object[] { id }, cancellationToken);
        if (contest != null)
        {
            contest.Cancel();
            _contests.Update(contest);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ActivateSegmentAsync(
        Guid contestId,
        Guid segmentId,
        CancellationToken cancellationToken = default)
    {
        var contest = await GetContestByIdAsync(contestId, cancellationToken);
        if (contest is null) return false;
        var parent = contest.Segments.FirstOrDefault(segment =>
            segment.Id == segmentId ||
            segment.NestedSegments.Any(nested => nested.Id == segmentId));
        if (parent is null) return false;
        var target = parent.Id == segmentId
            ? parent
            : parent.NestedSegments.First(segment => segment.Id == segmentId);

        foreach (var active in contest.Segments.Where(segment =>
                     segment.Status == SegmentStatus.InProgress &&
                     segment.Id != parent.Id))
        {
            foreach (var nested in active.NestedSegments.Where(segment =>
                         segment.Status == SegmentStatus.InProgress))
            {
                nested.End();
            }
            active.End();
        }
        foreach (var active in parent.NestedSegments.Where(segment =>
                     segment.Status == SegmentStatus.InProgress &&
                     segment.Id != target.Id))
        {
            active.End();
        }
        if (parent.Status == SegmentStatus.Pending) parent.Start();
        if (target.Status == SegmentStatus.Pending) target.Start();
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
