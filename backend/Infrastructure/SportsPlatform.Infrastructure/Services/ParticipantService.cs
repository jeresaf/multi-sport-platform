using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface IParticipantService
{
    Task<Athlete> CreateAthleteAsync(Guid tenantId, string name, string shortName, CancellationToken cancellationToken = default);
    Task<Team> CreateTeamAsync(Guid tenantId, string name, string shortName, CancellationToken cancellationToken = default);
    Task<Participant?> GetParticipantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetParticipantsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task RegisterAthleteForSportAsync(Guid athleteId, Guid sportId, CancellationToken cancellationToken = default);
    Task<TeamRoster> CreateRosterAsync(Guid teamId, string name, Guid? competitionId, CancellationToken cancellationToken = default);
    Task AddRosterEntryAsync(Guid rosterId, Guid athleteId, string? jerseyNumber, string? position, CancellationToken cancellationToken = default);
}

public class ParticipantService : IParticipantService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Participant> _participants;
    private readonly DbSet<Athlete> _athletes;
    private readonly DbSet<Team> _teams;
    private readonly DbSet<TeamRoster> _rosters;

    public ParticipantService(SportsPlatformDbContext context)
    {
        _context = context;
        _participants = context.Set<Participant>();
        _athletes = context.Set<Athlete>();
        _teams = context.Set<Team>();
        _rosters = context.Set<TeamRoster>();
    }

    public async Task<Athlete> CreateAthleteAsync(Guid tenantId, string name, string shortName, CancellationToken cancellationToken = default)
    {
        var athlete = new Athlete(Guid.NewGuid(), tenantId, name, shortName);
        _athletes.Add(athlete);
        await _context.SaveChangesAsync(cancellationToken);
        return athlete;
    }

    public async Task<Team> CreateTeamAsync(Guid tenantId, string name, string shortName, CancellationToken cancellationToken = default)
    {
        var team = new Team(Guid.NewGuid(), tenantId, name, shortName);
        _teams.Add(team);
        await _context.SaveChangesAsync(cancellationToken);
        return team;
    }

    public async Task<Participant?> GetParticipantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _participants.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<Participant>> GetParticipantsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _participants
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RegisterAthleteForSportAsync(Guid athleteId, Guid sportId, CancellationToken cancellationToken = default)
    {
        var athlete = await _athletes.FindAsync(new object[] { athleteId }, cancellationToken);
        if (athlete == null)
            throw new InvalidOperationException($"Athlete with ID {athleteId} not found.");

        athlete.RegisterForSport(sportId);
        _athletes.Update(athlete);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TeamRoster> CreateRosterAsync(Guid teamId, string name, Guid? competitionId, CancellationToken cancellationToken = default)
    {
        var team = await _teams.FindAsync(new object[] { teamId }, cancellationToken);
        if (team == null)
            throw new InvalidOperationException($"Team with ID {teamId} not found.");

        var roster = team.AddRoster(name, competitionId);
        _teams.Update(team);
        await _context.SaveChangesAsync(cancellationToken);
        return roster;
    }

    public async Task AddRosterEntryAsync(Guid rosterId, Guid athleteId, string? jerseyNumber, string? position, CancellationToken cancellationToken = default)
    {
        var roster = await _rosters
            .Include(r => r.Entries)
            .FirstOrDefaultAsync(r => r.Id == rosterId, cancellationToken);
        if (roster == null)
            throw new InvalidOperationException($"Roster with ID {rosterId} not found.");

        roster.AddAthlete(athleteId, jerseyNumber, position);
        _rosters.Update(roster);
        await _context.SaveChangesAsync(cancellationToken);
    }
}