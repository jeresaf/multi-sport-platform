using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

/// <summary>
/// <c>FAILED</c> → <c>PENDING</c> for one message (docs/domain/state-machines.md, "Outbox message"); the processor
/// delivers it on its next poll, then the messages of its aggregate it held back. The audit entry (with the reason)
/// commits with the re-drive in the target module's outbox; a re-drive that changes nothing is audited as a failure
/// by the Host.
/// </summary>
public sealed class RedriveOutboxMessageHandler(OutboxDiagnostics diagnostics, AuditScope audit)
{
    public const string AuditEntityType = "OutboxMessage";

    public async Task<Result<OutboxMessageResponse>> HandleAsync(string module, Guid messageId, string reason, CancellationToken cancellationToken)
    {
        audit.Describe(AuditEntityType, messageId, reason, new RedriveAuditMetadata(module));
        var result = await diagnostics.RedriveAsync(module, messageId, audit.CreateRecord(AuditOutcome.Success), cancellationToken);
        if (result.Outcome == OutboxRedriveOutcome.Redriven)
        {
            audit.MarkRecorded();
        }

        return result.Outcome switch
        {
            OutboxRedriveOutcome.Redriven => Result.Success(OutboxMessageResponse.From(result.Message!)),
            OutboxRedriveOutcome.NotFailed => PlatformErrors.OutboxMessageNotFailed,
            _ => PlatformErrors.OutboxMessageNotFound,
        };
    }
}

/// <summary>The module schema whose outbox holds the message.</summary>
public sealed record RedriveAuditMetadata(string Module);
