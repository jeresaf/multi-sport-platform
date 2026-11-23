using FluentValidation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListOrganisationsRequestValidator : AbstractValidator<ListOrganisationsRequest>
{
    public ListOrganisationsRequestValidator()
    {
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length <= ListOrganisationsRequest.SearchMaxLength)
            .WithMessage($"'q' is at most {ListOrganisationsRequest.SearchMaxLength} characters.")
            // PostgreSQL refuses a NUL in text, and no name holds a control character.
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("'q' can't contain control characters.");
        RuleFor(request => request.Type)
            .Must(type => type is null || ListOrganisationsRequest.TryParseType(type, out _))
            .WithMessage($"'type' must be one of: {string.Join(", ", ListOrganisationsRequest.Types)}.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListOrganisationsRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListOrganisationsRequest.Statuses)}.");
        RuleFor(request => request.ParentId)
            .Must(parentId => parentId is null || ListOrganisationsRequest.TryParseParentId(parentId, out _))
            .WithMessage("'parentId' must be an organisation ID (a UUID).");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListOrganisationsRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListOrganisationsRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || ListOrganisationsRequest.TryParseCursor(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
