using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

/// <summary>
/// <c>FAILED</c> → <c>PENDING</c> for one message (docs/domain/state-machines.md, "Outbox message"); the processor
/// delivers it on its next poll, then the messages of its aggregate it held back.
/// </summary>
public sealed class RedriveOutboxMessageHandler(OutboxDiagnostics diagnostics)
{
    /// <summary>Re-drives the message; <paramref name="reason"/> is validated and is recorded by the audit (S01-T6).</summary>
    public async Task<Result<OutboxMessageResponse>> HandleAsync(string module, Guid messageId, string reason, CancellationToken cancellationToken)
    {
        var result = await diagnostics.RedriveAsync(module, messageId, cancellationToken);
        return result.Outcome switch
        {
            OutboxRedriveOutcome.Redriven => Result.Success(OutboxMessageResponse.From(result.Message!)),
            OutboxRedriveOutcome.NotFailed => PlatformErrors.OutboxMessageNotFailed,
            _ => PlatformErrors.OutboxMessageNotFound,
        };
    }
}
