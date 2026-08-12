using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Sports;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public class SportsService : ISportsService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Sport> _sports;

    public SportsService(SportsPlatformDbContext context)
    {
        _context = context;
        _sports = context.Set<Sport>();
    }

    public async Task<Sport> CreateSportAsync(string code, string name, string? description, CancellationToken cancellationToken = default, string? icon = null, IReadOnlyCollection<string>? supportedParticipantShapes = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Sport code is required.", nameof(code));
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (await _sports.AnyAsync(item => item.Code == normalizedCode, cancellationToken))
            throw new InvalidOperationException($"A sport with code '{code}' already exists.");
        var sport = new Sport(Guid.NewGuid(), code, name);
        sport.UpdateDetails(name, description, icon, supportedParticipantShapes ?? new[] { "team" });
        _sports.Add(sport);
        await _context.SaveChangesAsync(cancellationToken);
        return sport;
    }

    public async Task<Sport?> GetSportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _sports
            .Include(sport => sport.Versions)
            .FirstOrDefaultAsync(sport => sport.Id == id, cancellationToken);
    }

    public async Task<Sport?> GetSportByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _sports
            .Include(sport => sport.Versions)
            .FirstOrDefaultAsync(s => s.Code == code.Trim().ToUpperInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<Sport>> GetAllSportsAsync(CancellationToken cancellationToken = default)
    {
        return await _sports
            .Include(sport => sport.Versions)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateSportAsync(Sport sport, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(sport).State == EntityState.Detached)
            _sports.Update(sport);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSport>> GetTenantSportsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _context.Set<TenantSport>()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<TenantSport> SetTenantSportEnabledAsync(Guid tenantId, Guid sportId, bool enabled, CancellationToken cancellationToken = default)
    {
        var sport = await _sports.SingleOrDefaultAsync(item => item.Id == sportId, cancellationToken)
            ?? throw new InvalidOperationException($"Sport with ID {sportId} not found.");
        if (enabled && !sport.IsActive)
            throw new InvalidOperationException("An inactive sport cannot be enabled for a tenant.");

        var tenantSport = await _context.Set<TenantSport>().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.SportId == sportId,
            cancellationToken);
        if (tenantSport is null)
        {
            tenantSport = new TenantSport(Guid.NewGuid(), tenantId, sportId);
            if (!enabled) tenantSport.Disable();
            _context.Add(tenantSport);
        }
        else if (enabled)
            tenantSport.Enable();
        else
            tenantSport.Disable();
        await _context.SaveChangesAsync(cancellationToken);
        return tenantSport;
    }

    public async Task<SportDefinitionVersion> CreateSportVersionAsync(Guid sportId, string name, string createdBy, CancellationToken cancellationToken = default)
    {
        var sport = await _sports
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == sportId, cancellationToken);
        if (sport == null)
            throw new InvalidOperationException($"Sport with ID {sportId} not found.");

        var version = sport.CreateNewVersion(name, createdBy);
        _context.Entry(version).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<SportDefinitionVersion?> GetSportVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var sport = await _sports.Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Versions.Any(version => version.Id == versionId), cancellationToken);
        return sport?.Versions.Single(item => item.Id == versionId);
    }

    public async Task<SportDefinitionVersion> CloneSportVersionAsync(
        Guid sportId,
        Guid sourceVersionId,
        string name,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var sport = await _sports.Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == sportId, cancellationToken)
            ?? throw new InvalidOperationException($"Sport with ID {sportId} not found.");
        var clone = sport.CloneVersion(sourceVersionId, name, createdBy);
        _context.Entry(clone).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return clone;
    }

    public async Task ConfigureSportVersionAsync(
        Guid versionId,
        SportDefinitionConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var version = await GetSportVersionAsync(versionId, cancellationToken)
            ?? throw new InvalidOperationException($"Version with ID {versionId} not found.");
        version.ConfigureContestShape(
            configuration.ContestShape,
            configuration.ParticipantType,
            configuration.SegmentDefinitions,
            configuration.EventDefinitions,
            configuration.MetricDefinitions,
            configuration.ResultMethods,
            configuration.RankingRules,
            configuration.DisplayConfiguration,
            configuration.ValidationConfiguration);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishSportVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var sport = await _sports
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(
                item => item.Versions.Any(version => version.Id == versionId),
                cancellationToken);
        var version = sport?.Versions.FirstOrDefault(item => item.Id == versionId);
        if (version is null)
            throw new InvalidOperationException($"Version with ID {versionId} not found.");

        version.Publish();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
