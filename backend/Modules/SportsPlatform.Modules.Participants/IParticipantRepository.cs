using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Modules.Participants;

public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Athlete?> GetAthleteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Team?> GetTeamByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Participant?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetByTypeAsync(ParticipantType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Participant>> GetByStatusAsync(ParticipantStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Athlete>> GetAthletesBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsBySportIdAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<SportRegistration?> GetRegistrationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SportRegistration>> GetRegistrationsByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<TeamRoster?> GetRosterByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamRoster>> GetRostersByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    void Add(Participant participant);
    void Update(Participant participant);
    void AddRegistration(SportRegistration registration);
    void UpdateRegistration(SportRegistration registration);
    void AddRoster(TeamRoster roster);
    void UpdateRoster(TeamRoster roster);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}