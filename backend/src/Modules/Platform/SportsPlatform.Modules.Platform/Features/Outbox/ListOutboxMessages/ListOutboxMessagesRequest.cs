using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

/// <summary>
/// The list's query parameters as sent, checked by <see cref="ListOutboxMessagesRequestValidator"/>. Every parameter is
/// bound as text, so a non-number <c>limit</c> gets the same <c>VALIDATION_INVALID_QUERY</c> as an out-of-range one.
/// </summary>
public sealed record ListOutboxMessagesRequest(
    [FromQuery(Name = "module")] string? Module,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    private static readonly Dictionary<string, OutboxMessageStatus> _statuses = Enum.GetValues<OutboxMessageStatus>()
        .ToDictionary(status => MessageSerialization.EnumNamingPolicy.ConvertName(status.ToString()), StringComparer.Ordinal);

    /// <summary>The accepted <c>status</c> values: <c>PENDING</c>, <c>PROCESSED</c>, <c>FAILED</c>.</summary>
    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseStatus(string value, out OutboxMessageStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public OutboxQuery ToQuery()
    {
        OutboxMessageStatus? status = Status is null ? null
            : TryParseStatus(Status, out var parsed) ? parsed : throw new InvalidOperationException("Validate the request first.");
        var limit = Limit is null ? DefaultLimit
            : TryParseLimit(Limit, out var value) ? value : throw new InvalidOperationException("Validate the request first.");
        Guid? before = Cursor is null ? null
            : OutboxCursor.TryDecode(Cursor, out var lastId) ? lastId : throw new InvalidOperationException("Validate the request first.");
        return new OutboxQuery(Module, status, before, limit);
    }
}
