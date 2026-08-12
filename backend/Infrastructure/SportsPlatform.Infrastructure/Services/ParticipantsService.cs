using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface IParticipantsService
{
    Task<Team> CreateTeamAsync(string name, string shortName, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Athlete> CreateAthleteAsync(string name, string shortName, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Participant> CreateParticipantAsync(string name, string shortName, Guid tenantId, ParticipantType type, CancellationToken cancellationToken = default);
    Task<ParticipantSportProfile> SetSportAttributesAsync(Guid participantId, Guid sportId, string attributesJson, CancellationToken cancellationToken = default);
    Task<Athlete?> GetAthleteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrganizationParticipant?> GetOrganizationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetRegistrationHistoryAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<bool> OrganizationDuplicateExistsAsync(Guid tenantId, string name, string shortName, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<TeamRoster> CreateSeasonalRosterAsync(Guid teamId, string name, string season, DateTime? startsAt, DateTime? endsAt, Guid? competitionId, CancellationToken cancellationToken = default);
    Task TransferAthleteAsync(Guid teamId, Guid targetRosterId, Guid athleteId, string? jerseyNumber, string? position, string reason, CancellationToken cancellationToken = default);
    Task RegisterAthleteForSportAsync(Guid athleteId, Guid sportId, CancellationToken cancellationToken = default);
    Task<Participant?> GetParticipantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetParticipantsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetParticipantsByTypeAsync(Guid tenantId, ParticipantType participantType, CancellationToken cancellationToken = default);
    Task UpdateParticipantAsync(Participant participant, CancellationToken cancellationToken = default);
}

public class ParticipantsService : IParticipantsService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Participant> _participants;

    public ParticipantsService(SportsPlatformDbContext context)
    {
        _context = context;
        _participants = context.Set<Participant>();
    }

    public async Task<Team> CreateTeamAsync(string name, string shortName, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await OrganizationDuplicateExistsAsync(tenantId, name, shortName, cancellationToken: cancellationToken))
            throw new InvalidOperationException("A team or club with the same name or short name already exists.");
        var team = new Team(Guid.NewGuid(), tenantId, name, shortName);
        _participants.Add(team);
        await _context.SaveChangesAsync(cancellationToken);
        return team;
    }

    public async Task<Athlete> CreateAthleteAsync(string name, string shortName, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var athlete = new Athlete(Guid.NewGuid(), tenantId, name, shortName);
        _participants.Add(athlete);
        await _context.SaveChangesAsync(cancellationToken);
        return athlete;
    }

    public async Task<Participant> CreateParticipantAsync(
        string name, string shortName, Guid tenantId, ParticipantType type,
        CancellationToken cancellationToken = default)
    {
        if (type is ParticipantType.Team or ParticipantType.Club
            && await OrganizationDuplicateExistsAsync(
                tenantId, name, shortName, cancellationToken: cancellationToken))
            throw new InvalidOperationException("A team or club with the same name or short name already exists.");
        Participant participant = type switch
        {
            ParticipantType.Team => new Team(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.Athlete => new Athlete(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.Pair => new Pair(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.RelayTeam => new RelayTeam(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.Crew => new Crew(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.Club => new Club(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.School => new School(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.Country => new Country(Guid.NewGuid(), tenantId, name, shortName),
            ParticipantType.CompositeTeam => new CompositeTeam(Guid.NewGuid(), tenantId, name, shortName),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        _participants.Add(participant);
        await _context.SaveChangesAsync(cancellationToken);
        return participant;
    }

    public async Task<ParticipantSportProfile> SetSportAttributesAsync(
        Guid participantId, Guid sportId, string attributesJson,
        CancellationToken cancellationToken = default)
    {
        var participant = await _participants
            .Include(item => item.SportProfiles)
            .SingleOrDefaultAsync(item => item.Id == participantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Participant with ID {participantId} was not found.");
        var isNew = participant.SportProfiles.All(item => item.SportId != sportId);
        var profile = participant.SetSportAttributes(sportId, attributesJson);
        if (isNew)
            _context.Entry(profile).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<Participant?> GetParticipantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _participants.Include(item => item.SportProfiles)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<Athlete?> GetAthleteByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await _participants.OfType<Athlete>()
            .Include(item => item.SportProfiles)
            .Include(item => item.SportRegistrations)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<OrganizationParticipant?> GetOrganizationByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var team = await _participants.OfType<Team>()
            .Include(item => item.Rosters)
            .ThenInclude(item => item.Entries)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (team is not null) return team;
        return await _participants.OfType<Club>()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetRegistrationHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default) =>
        await _context.Registrations.AsNoTracking()
            .Where(item => item.ParticipantId == participantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> OrganizationDuplicateExistsAsync(
        Guid tenantId,
        string name,
        string shortName,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();
        var normalizedShortName = shortName.Trim().ToLower();
        return _participants.AnyAsync(item =>
            item.TenantId == tenantId
            && (item.Type == ParticipantType.Team || item.Type == ParticipantType.Club)
            && (!excludeId.HasValue || item.Id != excludeId.Value)
            && (item.Name.ToLower() == normalizedName
                || item.ShortName.ToLower() == normalizedShortName), cancellationToken);
    }

    public async Task<TeamRoster> CreateSeasonalRosterAsync(
        Guid teamId,
        string name,
        string season,
        DateTime? startsAt,
        DateTime? endsAt,
        Guid? competitionId,
        CancellationToken cancellationToken = default)
    {
        var team = await _participants.OfType<Team>()
            .Include(item => item.Rosters)
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team with ID {teamId} was not found.");
        if (competitionId.HasValue && !await _context.Competitions.AnyAsync(item =>
                item.Id == competitionId.Value && item.TenantId == team.TenantId, cancellationToken))
            throw new ArgumentException("The selected competition was not found in this tenant.", nameof(competitionId));
        var roster = team.AddRoster(name, season, startsAt, endsAt, competitionId);
        _context.Entry(roster).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
        return roster;
    }

    public async Task TransferAthleteAsync(
        Guid teamId,
        Guid targetRosterId,
        Guid athleteId,
        string? jerseyNumber,
        string? position,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A movement reason is required.", nameof(reason));
        var team = await _participants.OfType<Team>()
            .Include(item => item.Rosters)
            .ThenInclude(item => item.Entries)
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team with ID {teamId} was not found.");
        var athlete = await _participants.OfType<Athlete>()
            .SingleOrDefaultAsync(item => item.Id == athleteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Athlete with ID {athleteId} was not found.");
        if (athlete.TenantId != team.TenantId)
            throw new InvalidOperationException("Athlete and team must belong to the same tenant.");
        var target = team.Rosters.SingleOrDefault(item => item.Id == targetRosterId)
            ?? throw new KeyNotFoundException($"Roster with ID {targetRosterId} was not found for this team.");
        if (target.Entries.Any(item => item.AthleteId == athleteId && item.IsActive))
            throw new InvalidOperationException("The athlete is already active on the target roster.");
        foreach (var roster in team.Rosters.Where(item => item.Id != targetRosterId))
        {
            var active = roster.Entries.SingleOrDefault(item => item.AthleteId == athleteId && item.IsActive);
            if (active is not null) active.Deactivate(reason);
        }
        var entry = target.AddAthlete(athleteId, jerseyNumber, position);
        _context.Entry(entry).State = EntityState.Added;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterAthleteForSportAsync(
        Guid athleteId, Guid sportId, CancellationToken cancellationToken = default)
    {
        var athlete = await _participants.OfType<Athlete>()
            .Include(item => item.SportRegistrations)
            .SingleOrDefaultAsync(item => item.Id == athleteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Athlete with ID {athleteId} was not found.");
        if (!await _context.Sports.AnyAsync(item => item.Id == sportId && item.IsActive, cancellationToken))
            throw new ArgumentException("The selected active sport was not found.", nameof(sportId));
        var isNew = athlete.SportRegistrations.All(item => item.SportId != sportId || !item.IsActive);
        athlete.RegisterForSport(sportId);
        if (isNew)
        {
            var registration = athlete.SportRegistrations.Single(item => item.SportId == sportId && item.IsActive);
            _context.Entry(registration).State = EntityState.Added;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Participant>> GetParticipantsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _participants.Include(item => item.SportProfiles)
            .Where(p => p.TenantId == tenantId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Participant>> GetParticipantsByTypeAsync(Guid tenantId, ParticipantType participantType, CancellationToken cancellationToken = default)
    {
        return await _participants.Include(item => item.SportProfiles)
            .Where(p => p.TenantId == tenantId && p.Type == participantType)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateParticipantAsync(Participant participant, CancellationToken cancellationToken = default)
    {
        _participants.Update(participant);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
