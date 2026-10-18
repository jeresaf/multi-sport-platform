using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

/// <summary>
/// The audit list's query parameters as sent, checked by <see cref="ListAuditEntriesRequestValidator"/>. Every parameter
/// is bound as text, so a malformed value gets <c>VALIDATION_INVALID_QUERY</c> like an out-of-range one.
/// </summary>
public sealed partial record ListAuditEntriesRequest(
    [FromQuery(Name = "tenantId")] string? TenantId,
    [FromQuery(Name = "actorId")] string? ActorId,
    [FromQuery(Name = "action")] string? Action,
    [FromQuery(Name = "entityType")] string? EntityType,
    [FromQuery(Name = "entityId")] string? EntityId,
    [FromQuery(Name = "correlationId")] string? CorrelationId,
    [FromQuery(Name = "outcome")] string? Outcome,
    [FromQuery(Name = "from")] string? From,
    [FromQuery(Name = "to")] string? To,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    public const int CorrelationIdMaxLength = 64;

    private static readonly Dictionary<string, AuditOutcome> _outcomes = Enum.GetValues<AuditOutcome>()
        .ToDictionary(outcome => MessageSerialization.EnumNamingPolicy.ConvertName(outcome.ToString()), StringComparer.Ordinal);

    /// <summary>The accepted <c>outcome</c> values: <c>SUCCESS</c>, <c>FAILURE</c>, <c>DENIED</c>.</summary>
    public static IReadOnlyCollection<string> Outcomes => _outcomes.Keys;

    public static bool TryParseId(string value, out Guid id) => Guid.TryParseExact(value, "D", out id);

    public static bool TryParseOutcome(string value, out AuditOutcome outcome) => _outcomes.TryGetValue(value, out outcome);

    /// <summary>An ISO 8601 instant with an offset (<c>2026-10-17T09:00:00Z</c>), as UTC.</summary>
    public static bool TryParseInstant(string value, out DateTimeOffset instant)
    {
        instant = default;
        if (!InstantFormat().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return false;
        }

        instant = parsed.ToUniversalTime();
        return true;
    }

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public AuditQuery ToQuery()
    {
        DateTimeOffset? cursorAt = null;
        Guid? cursorId = null;
        if (Cursor is not null)
        {
            if (!AuditCursor.TryDecode(Cursor, out var at, out var id))
            {
                throw NotValidated();
            }

            (cursorAt, cursorId) = (at, id);
        }

        return new AuditQuery(
            Parse<Guid>(TenantId, TryParseId),
            Parse<Guid>(ActorId, TryParseId),
            Action,
            EntityType,
            Parse<Guid>(EntityId, TryParseId),
            CorrelationId,
            Parse<AuditOutcome>(Outcome, TryParseOutcome),
            Parse<DateTimeOffset>(From, TryParseInstant),
            Parse<DateTimeOffset>(To, TryParseInstant),
            Parse<int>(Limit, TryParseLimit) ?? DefaultLimit,
            cursorAt,
            cursorId);
    }

    private delegate bool TryParse<T>(string value, out T parsed);

    private static T? Parse<T>(string? value, TryParse<T> tryParse)
        where T : struct =>
        value is null ? null : tryParse(value, out var parsed) ? parsed : throw NotValidated();

    private static InvalidOperationException NotValidated() => new("Validate the request first.");

    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(\\.\\d{1,7})?(Z|[+-]\\d{2}:\\d{2})$")]
    private static partial Regex InstantFormat();
}
