using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.BuildingBlocks.Auditing;

/// <summary>
/// Builds the integration event that carries an <see cref="AuditRecord"/> through a module's outbox to the Platform
/// module, which declares the event (<c>AuditRecorded.V1</c>) and implements this interface.
/// </summary>
public interface IAuditEventFactory
{
    /// <summary>The aggregate type of audit outbox messages; each entry is its own aggregate, so none waits for another.</summary>
    const string AggregateType = "AuditEntry";

    IIntegrationEvent Create(AuditRecord record);
}
