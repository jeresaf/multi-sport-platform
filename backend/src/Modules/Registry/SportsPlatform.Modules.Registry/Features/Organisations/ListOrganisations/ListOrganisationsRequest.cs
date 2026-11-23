using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; no status means every state but <c>ARCHIVED</c>;
/// <paramref name="After"/> is the sort key of the previous page's last organisation.
/// </summary>
public sealed record OrganisationsQuery(
    string? Search, OrganisationType? Type, OrganisationStatus? Status, int Limit, DirectoryCursor? After);

/// <summary>The directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListOrganisationsRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "type")] string? Type,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    public const int SearchMaxLength = 100;

    private static readonly Dictionary<string, OrganisationType> _types = ValuesOf<OrganisationType>();
    private static readonly Dictionary<string, OrganisationStatus> _statuses = ValuesOf<OrganisationStatus>();

    public static IReadOnlyCollection<string> Types => _types.Keys;

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseType(string value, out OrganisationType type) => _types.TryGetValue(value, out type);

    public static bool TryParseStatus(string value, out OrganisationStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    public static bool TryParseCursor(string value, out DirectoryCursor cursor) =>
        DirectoryCursor.TryDecode(value, OrganisationDetails.DisplayNameMaxLength, out cursor);

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public OrganisationsQuery ToQuery()
    {
        OrganisationType? type = null;
        if (Type is not null)
        {
            type = TryParseType(Type, out var parsed) ? parsed : throw NotValidated();
        }

        OrganisationStatus? status = null;
        if (Status is not null)
        {
            status = TryParseStatus(Status, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        DirectoryCursor? after = null;
        if (Cursor is not null)
        {
            after = TryParseCursor(Cursor, out var cursor) ? cursor : throw NotValidated();
        }

        return new OrganisationsQuery(string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(), type, status, limit, after);
    }

    private static Dictionary<string, T> ValuesOf<T>()
        where T : struct, Enum =>
        Enum.GetValues<T>().ToDictionary(
            value => MessageSerialization.EnumNamingPolicy.ConvertName(value.ToString()), StringComparer.Ordinal);

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
