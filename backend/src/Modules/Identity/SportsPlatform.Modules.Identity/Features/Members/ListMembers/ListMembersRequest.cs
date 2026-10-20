using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Members.ListMembers;

/// <summary><paramref name="After"/> is the last membership ID of the previous page; no status means not <c>REMOVED</c>.</summary>
public sealed record MemberQuery(MembershipStatus? Status, int Limit, Guid? After);

/// <summary>The member list's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListMembersRequest(
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    private static readonly Dictionary<string, MembershipStatus> _statuses = Enum.GetValues<MembershipStatus>()
        .ToDictionary(status => MessageSerialization.EnumNamingPolicy.ConvertName(status.ToString()), StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseStatus(string value, out MembershipStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The opaque cursor: the last membership ID of a page (UUIDv7, the sort key).</summary>
    public static string EncodeCursor(Guid membershipId) => IdCursor.Encode(membershipId);

    public static bool TryDecodeCursor(string? cursor, out Guid membershipId) => IdCursor.TryDecode(cursor, out membershipId);

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public MemberQuery ToQuery()
    {
        MembershipStatus? status = null;
        if (Status is not null)
        {
            status = TryParseStatus(Status, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        Guid? after = null;
        if (Cursor is not null)
        {
            after = TryDecodeCursor(Cursor, out var id) ? id : throw NotValidated();
        }

        return new MemberQuery(status, limit, after);
    }

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
