using SportsPlatform.Modules.Audit.Entities;

namespace SportsPlatform.Modules.Audit;

public interface IAuditService
{
    Task RecordAsync(
        string action,
        string entityType,
        string entityId,
        Guid? tenantId = null,
        Guid? userId = null,
        string? originalValue = null,
        string? newValue = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntry>> GetByTenantAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}