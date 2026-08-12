using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface ICompetitionsService
{
    Task<Competition> CreateCompetitionAsync(string name, Guid sportId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Competition?> GetCompetitionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Competition>> GetCompetitionsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Competition>> GetCompetitionsBySportAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task UpdateCompetitionAsync(Competition competition, CancellationToken cancellationToken = default);
    Task PublishCompetitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task ArchiveCompetitionAsync(Guid id, CancellationToken cancellationToken = default);
}

public class CompetitionsService : ICompetitionsService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Competition> _competitions;

    public CompetitionsService(SportsPlatformDbContext context)
    {
        _context = context;
        _competitions = context.Set<Competition>();
    }

    public async Task<Competition> CreateCompetitionAsync(string name, Guid sportId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var competition = new Competition(Guid.NewGuid(), tenantId, name, sportId);
        var rulesetVersionId = await _context.Sports
            .AsNoTracking()
            .Where(sport => sport.Id == sportId)
            .SelectMany(sport => sport.Versions)
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (rulesetVersionId.HasValue)
            competition.AssignRulesetVersion(rulesetVersionId.Value);
        _competitions.Add(competition);
        await _context.SaveChangesAsync(cancellationToken);
        return competition;
    }

    public async Task<Competition?> GetCompetitionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _competitions
            .AsSplitQuery()
            .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                    .ThenInclude(item => item.Groups)
            .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                    .ThenInclude(item => item.Rounds)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Competition>> GetCompetitionsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _competitions.Where(c => c.TenantId == tenantId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Competition>> GetCompetitionsBySportAsync(Guid sportId, CancellationToken cancellationToken = default)
    {
        return await _competitions.Where(c => c.SportId == sportId).ToListAsync(cancellationToken);
    }

    public async Task UpdateCompetitionAsync(Competition competition, CancellationToken cancellationToken = default)
    {
        _competitions.Update(competition);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishCompetitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var competition = await _competitions.FindAsync(new object[] { id }, cancellationToken);
        if (competition != null)
        {
            competition.Publish();
            _competitions.Update(competition);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ArchiveCompetitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var competition = await _competitions.FindAsync(new object[] { id }, cancellationToken);
        if (competition != null)
        {
            competition.Archive();
            _competitions.Update(competition);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
