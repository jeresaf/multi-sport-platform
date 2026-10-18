namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

/// <summary>Why the message is re-driven; the audit entry records it (action audit <c>REQUIRED_WITH_REASON</c>).</summary>
public sealed record RedriveOutboxMessageRequest(string? Reason)
{
    public const int MaxReasonLength = 500;
}
