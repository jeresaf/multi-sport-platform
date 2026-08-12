using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports;

public interface ISportsService
{
    Task<Sport> CreateSportAsync(string code, string name, string? description, CancellationToken cancellationToken = default, string? icon = null, IReadOnlyCollection<string>? supportedParticipantShapes = null);
    Task<Sport?> GetSportByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sport?> GetSportByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sport>> GetAllSportsAsync(CancellationToken cancellationToken = default);
    Task UpdateSportAsync(Sport sport, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSport>> GetTenantSportsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSport> SetTenantSportEnabledAsync(Guid tenantId, Guid sportId, bool enabled, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion> CreateSportVersionAsync(Guid sportId, string name, string createdBy, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion?> GetSportVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
    Task<SportDefinitionVersion> CloneSportVersionAsync(Guid sportId, Guid sourceVersionId, string name, string createdBy, CancellationToken cancellationToken = default);
    Task ConfigureSportVersionAsync(Guid versionId, SportDefinitionConfiguration configuration, CancellationToken cancellationToken = default);
    Task PublishSportVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
}

public sealed record SportDefinitionConfiguration(
    string ContestShape,
    string ParticipantType,
    string SegmentDefinitions,
    string EventDefinitions,
    string MetricDefinitions,
    string ResultMethods,
    string RankingRules,
    string DisplayConfiguration,
    string ValidationConfiguration);
