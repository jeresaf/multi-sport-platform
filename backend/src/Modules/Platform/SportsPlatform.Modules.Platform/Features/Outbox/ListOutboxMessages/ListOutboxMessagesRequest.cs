using System.Globalization;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

/// <summary>The list's query parameters as sent; <see cref="Validate"/> turns them into an <see cref="OutboxQuery"/>.</summary>
/// <remarks>
/// <c>limit</c> is bound as text, so a non-number gets the same <c>VALIDATION_INVALID_QUERY</c> as an out-of-range one.
/// </remarks>
public sealed record ListOutboxMessagesRequest(string? Module, string? Status, string? Limit, string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    private static readonly Dictionary<string, OutboxMessageStatus> _statuses = Enum.GetValues<OutboxMessageStatus>()
        .ToDictionary(status => MessageSerialization.EnumNamingPolicy.ConvertName(status.ToString()), StringComparer.Ordinal);

    /// <summary>The query, or the reason a parameter is invalid (the Problem Details <c>detail</c>).</summary>
    public (OutboxQuery? Query, string? Problem) Validate(IReadOnlyCollection<string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        if (Module is not null && !modules.Contains(Module))
        {
            return (null, $"'module' must be one of: {string.Join(", ", modules)}.");
        }

        OutboxMessageStatus? status = null;
        if (Status is not null)
        {
            if (!_statuses.TryGetValue(Status, out var parsed))
            {
                return (null, $"'status' must be one of: {string.Join(", ", _statuses.Keys)}.");
            }

            status = parsed;
        }

        var limit = DefaultLimit;
        if (Limit is not null
            && (!int.TryParse(Limit, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > MaxLimit))
        {
            return (null, $"'limit' must be a whole number between 1 and {MaxLimit}.");
        }

        Guid? before = null;
        if (Cursor is not null)
        {
            if (!OutboxCursor.TryDecode(Cursor, out var lastId))
            {
                return (null, "'cursor' must be a nextCursor returned by this list.");
            }

            before = lastId;
        }

        return (new OutboxQuery(Module, status, before, limit), null);
    }
}
