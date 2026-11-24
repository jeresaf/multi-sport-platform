using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; no status means every state but <c>ARCHIVED</c>;
/// <paramref name="After"/> is the sort key, category rank and ID of the previous page's last team.
/// </summary>
public sealed record TeamsQuery(
    string? Search,
    string? Variant,
    Guid? OrganisationId,
    GenderCategory? GenderCategory,
    AgeGroup? AgeGroup,
    TeamLevel? Level,
    TeamStatus? Status,
    int Limit,
    TeamDirectoryCursor? After);

/// <summary>The directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListTeamsRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "variant")] string? Variant,
    [FromQuery(Name = "organisationId")] string? OrganisationId,
    [FromQuery(Name = "genderCategory")] string? GenderCategory,
    [FromQuery(Name = "ageGroup")] string? AgeGroup,
    [FromQuery(Name = "level")] string? Level,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    public const int SearchMaxLength = 100;

    private static readonly Dictionary<string, GenderCategory> _genderCategories = ValuesOf<GenderCategory>();
    private static readonly Dictionary<string, AgeGroup> _ageGroups = ValuesOf<AgeGroup>();
    private static readonly Dictionary<string, TeamLevel> _levels = ValuesOf<TeamLevel>();
    private static readonly Dictionary<string, TeamStatus> _statuses = ValuesOf<TeamStatus>();

    public static IReadOnlyCollection<string> GenderCategories => _genderCategories.Keys;

    public static IReadOnlyCollection<string> AgeGroups => _ageGroups.Keys;

    public static IReadOnlyCollection<string> Levels => _levels.Keys;

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseGenderCategory(string value, out GenderCategory gender) => _genderCategories.TryGetValue(value, out gender);

    public static bool TryParseAgeGroup(string value, out AgeGroup ageGroup) => _ageGroups.TryGetValue(value, out ageGroup);

    public static bool TryParseLevel(string value, out TeamLevel level) => _levels.TryGetValue(value, out level);

    public static bool TryParseStatus(string value, out TeamStatus status) => _statuses.TryGetValue(value, out status);

    /// <summary>A UUID in its usual form (32 hex digits with hyphens), as route IDs are written.</summary>
    public static bool TryParseOrganisationId(string value, out Guid organisationId) =>
        Guid.TryParseExact(value, "D", out organisationId);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public TeamsQuery ToQuery()
    {
        Guid? organisationId = null;
        if (OrganisationId is not null)
        {
            organisationId = TryParseOrganisationId(OrganisationId, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        TeamDirectoryCursor? after = null;
        if (Cursor is not null)
        {
            after = TeamDirectoryCursor.TryDecode(Cursor, out var cursor) ? cursor : throw NotValidated();
        }

        return new TeamsQuery(
            string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(),
            Variant,
            organisationId,
            Parsed<GenderCategory>(GenderCategory, TryParseGenderCategory),
            Parsed<AgeGroup>(AgeGroup, TryParseAgeGroup),
            Parsed<TeamLevel>(Level, TryParseLevel),
            Parsed<TeamStatus>(Status, TryParseStatus),
            limit,
            after);
    }

    private delegate bool TryParse<T>(string value, out T parsed);

    private static T? Parsed<T>(string? value, TryParse<T> tryParse)
        where T : struct =>
        value is null ? null : tryParse(value, out var parsed) ? parsed : throw NotValidated();

    private static Dictionary<string, T> ValuesOf<T>()
        where T : struct, Enum =>
        Enum.GetValues<T>().ToDictionary(
            value => MessageSerialization.EnumNamingPolicy.ConvertName(value.ToString()), StringComparer.Ordinal);

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
