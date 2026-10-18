namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

/// <summary>Why the message is re-driven; the audit entry records it (action audit <c>REQUIRED_WITH_REASON</c>).</summary>
public sealed record RedriveOutboxMessageRequest(string? Reason)
{
    public const int MaxReasonLength = 500;

    /// <summary>The trimmed reason, or the reason it is not usable (the Problem Details <c>detail</c>).</summary>
    public (string? Reason, string? Problem) Validate()
    {
        var reason = Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return (null, "'reason' must not be empty.");
        }

        return reason.Length > MaxReasonLength
            ? (null, $"'reason' must be at most {MaxReasonLength} characters.")
            : (reason, null);
    }
}
