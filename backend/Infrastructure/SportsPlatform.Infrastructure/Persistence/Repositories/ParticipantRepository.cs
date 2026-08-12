using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Participants;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.Repositories;

public sealed class ParticipantRepository : IParticipantRepository
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Participant> _participants;

    public ParticipantRepository(SportsPlatformDbContext context)
    {
        _context = context;
        _participants = context.Participants;
    }

    public Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _participants.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<Athlete?> GetAthleteByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _participants.OfType<Athlete>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<Team?> GetTeamByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _participants.OfType<Team>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    public Task<Participant?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default) =>
        _participants.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == name, cancellationToken);
    public async Task<IReadOnlyList<Participant>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _participants.Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Participant>> GetByTypeAsync(ParticipantType type, CancellationToken cancellationToken = default) =>
        await _participants.Where(item => item.Type == type).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Participant>> GetByStatusAsync(ParticipantStatus status, CancellationToken cancellationToken = default) =>
        await _participants.Where(item => item.Status == status).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Athlete>> GetAthletesBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default) =>
        await _participants.OfType<Athlete>()
            .Where(item => item.SportRegistrations.Any(registration => registration.SportId == sportId && registration.IsActive))
            .ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Team>> GetTeamsBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default)
    {
        var ids = _context.Set<SportRegistration>().Where(item => item.SportId == sportId && item.IsActive)
            .Select(item => item.AthleteId);
        return await _participants.OfType<Team>().Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
    }
    public Task<SportRegistration?> GetRegistrationByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<SportRegistration>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<SportRegistration>> GetRegistrationsByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        await _context.Set<SportRegistration>().Where(item => item.AthleteId == participantId).ToListAsync(cancellationToken);
    public Task<TeamRoster?> GetRosterByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<TeamRoster>().Include(item => item.Entries).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<TeamRoster>> GetRostersByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await _context.Set<TeamRoster>().Where(item => item.TeamId == teamId).ToListAsync(cancellationToken);
    public void Add(Participant participant) => _participants.Add(participant);
    public void Update(Participant participant) => _participants.Update(participant);
    public void AddRegistration(SportRegistration registration) => _context.Add(registration);
    public void UpdateRegistration(SportRegistration registration) => _context.Update(registration);
    public void AddRoster(TeamRoster roster) => _context.Add(roster);
    public void UpdateRoster(TeamRoster roster) => _context.Update(roster);
    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken) > 0;
}
