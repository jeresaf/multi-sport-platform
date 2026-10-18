using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Audit.AppendAuditEntry;

/// <summary>
/// Appends the entry an <c>AuditRecorded</c> message carries; the inbox makes a redelivery a no-op. On <c>sp_platform</c>,
/// so the entry's tenant need not be the envelope's, and the inbox row commits in the same transaction.
/// </summary>
internal sealed class AuditRecordedConsumer(PlatformAuditDbContext context) : IIntegrationEventConsumer<AuditRecorded.V1>
{
    public const string Name = "platform.audit";

    public Task HandleAsync(AuditRecorded.V1 integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        context.Add(AuditEntry.From(integrationEvent.ToRecord()));
        return Task.CompletedTask;
    }
}
