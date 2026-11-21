using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; <paramref name="City"/> the trimmed city as sent; no status means
/// every state but <c>ARCHIVED</c>; <paramref name="After"/> is the sort key of the previous page's last venue.
/// </summary>
public sealed record VenuesQuery(string? Search, string? Variant, string? City, VenueStatus? Status, int Limit, VenueCursor? After);

/// <summary>The directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListVenuesRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "variant")] string? Variant,
    [FromQuery(Name = "city")] string? City,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    public const int SearchMaxLength = 100;

    private static readonly Dictionary<string, VenueStatus> _statuses = Enum.GetValues<VenueStatus>().ToDictionary(
        value => MessageSerialization.EnumNamingPolicy.ConvertName(value.ToString()), StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseStatus(string value, out VenueStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public VenuesQuery ToQuery()
    {
        VenueStatus? status = null;
        if (Status is not null)
        {
            status = TryParseStatus(Status, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        VenueCursor? after = null;
        if (Cursor is not null)
        {
            after = VenueCursor.TryDecode(Cursor, out var cursor) ? cursor : throw NotValidated();
        }

        return new VenuesQuery(Trimmed(Q), Variant, Trimmed(City), status, limit, after);
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
