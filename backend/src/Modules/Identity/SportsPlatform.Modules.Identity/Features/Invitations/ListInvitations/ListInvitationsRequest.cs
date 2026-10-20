using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Features.Members.ListMembers;

namespace SportsPlatform.Modules.Identity.Features.Invitations.ListInvitations;

/// <summary>
/// <paramref name="After"/> is the last invitation ID of the previous page; no state means those still pending, expired
/// ones included (they can be sent again).
/// </summary>
public sealed record InvitationQuery(InvitationState? State, int Limit, Guid? After);

/// <summary>The invitation list's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListInvitationsRequest(
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    private static readonly Dictionary<string, InvitationState> _states = Enum.GetValues<InvitationState>()
        .ToDictionary(state => MessageSerialization.EnumNamingPolicy.ConvertName(state.ToString()), StringComparer.Ordinal);

    public static IReadOnlyCollection<string> States => _states.Keys;

    public static bool TryParseState(string value, out InvitationState state) => _states.TryGetValue(value, out state);

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public InvitationQuery ToQuery()
    {
        InvitationState? state = null;
        if (Status is not null)
        {
            state = TryParseState(Status, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = ListMembersRequest.DefaultLimit;
        if (Limit is not null && !ListMembersRequest.TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        Guid? after = null;
        if (Cursor is not null)
        {
            after = IdCursor.TryDecode(Cursor, out var id) ? id : throw NotValidated();
        }

        return new InvitationQuery(state, limit, after);
    }

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}
