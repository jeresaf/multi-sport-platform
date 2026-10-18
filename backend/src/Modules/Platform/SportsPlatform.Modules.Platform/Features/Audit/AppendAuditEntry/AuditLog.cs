using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Audit.AppendAuditEntry;

/// <summary>
/// Writes entries straight to <c>audit.audit_entries</c> in a scope of its own, so nothing the caller's DbContexts
/// track is saved with it. On <c>sp_platform</c>, so entries of any tenant, or of none, land under RLS.
/// </summary>
internal sealed class AuditLog(IServiceScopeFactory scopes) : IAuditLog
{
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set(record.CorrelationId, null);
        var context = scope.ServiceProvider.GetRequiredService<PlatformAuditDbContext>();
        context.Add(AuditEntry.From(record));
        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Builds <c>AuditRecorded.V1</c> for the modules' outboxes.</summary>
internal sealed class AuditEventFactory : IAuditEventFactory
{
    public IIntegrationEvent Create(AuditRecord record) => AuditRecorded.V1.From(record);
}
