using FluentValidation;

namespace SportsPlatform.Modules.Identity.Features.Members.ListMembers;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListMembersRequestValidator : AbstractValidator<ListMembersRequest>
{
    public ListMembersRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is null || ListMembersRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListMembersRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListMembersRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListMembersRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || ListMembersRequest.TryDecodeCursor(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
