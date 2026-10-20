using FluentValidation;
using SportsPlatform.Modules.Identity.Features.Members.ListMembers;

namespace SportsPlatform.Modules.Identity.Features.Invitations.ListInvitations;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListInvitationsRequestValidator : AbstractValidator<ListInvitationsRequest>
{
    public ListInvitationsRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is null || ListInvitationsRequest.TryParseState(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListInvitationsRequest.States)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListMembersRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListMembersRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || IdCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
