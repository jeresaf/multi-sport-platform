using SportsPlatform.BuildingBlocks.Auditing;

namespace SportsPlatform.Modules.Platform.Contracts.Audit;

/// <summary>
/// Appends an audit entry directly, outside any module transaction: authorization denials, failures that changed
/// nothing, and audited actions that save nothing (reads). Entries for changes go through the changing module's outbox
/// instead (<c>AuditRecorded</c>), so they commit with the change.
/// </summary>
public interface IAuditLog
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken);
}
