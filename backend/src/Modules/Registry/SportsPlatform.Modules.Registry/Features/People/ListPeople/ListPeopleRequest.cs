using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Registry.Contracts.People;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People.ListPeople;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; no status means every state but <c>ARCHIVED</c>;
/// <paramref name="After"/> is the sort key of the previous page's last person.
/// </summary>
public sealed record PeopleQuery(string? Search, PersonStatus? Status, AgeStatus? AgeStatus, int Limit, PersonCursor? After);

/// <summary>The directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListPeopleRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "ageStatus")] string? AgeStatus,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    public const int SearchMaxLength = 100;

    private static readonly Dictionary<string, PersonStatus> _statuses = Names<PersonStatus>();
    private static readonly Dictionary<string, AgeStatus> _ageStatuses = Names<AgeStatus>();

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static IReadOnlyCollection<string> AgeStatuses => _ageStatuses.Keys;

    public static bool TryParseStatus(string value, out PersonStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseAgeStatus(string value, out AgeStatus status) => _ageStatuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public PeopleQuery ToQuery()
    {
        PersonStatus? status = null;
        if (Status is not null)
        {
            status = TryParseStatus(Status, out var parsed) ? parsed : throw NotValidated();
        }

        AgeStatus? ageStatus = null;
        if (AgeStatus is not null)
        {
            ageStatus = TryParseAgeStatus(AgeStatus, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        PersonCursor? after = null;
        if (Cursor is not null)
        {
            after = PersonCursor.TryDecode(Cursor, out var cursor) ? cursor : throw NotValidated();
        }

        return new PeopleQuery(string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(), status, ageStatus, limit, after);
    }

    private static Dictionary<string, TEnum> Names<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(
            value => MessageSerialization.EnumNamingPolicy.ConvertName(value.ToString()), StringComparer.Ordinal);

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
