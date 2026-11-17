using FluentValidation;

namespace SportsPlatform.Modules.Registry.Features.People.ListPeople;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListPeopleRequestValidator : AbstractValidator<ListPeopleRequest>
{
    public ListPeopleRequestValidator()
    {
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length <= ListPeopleRequest.SearchMaxLength)
            .WithMessage($"'q' is at most {ListPeopleRequest.SearchMaxLength} characters.")
            // PostgreSQL refuses a NUL in text, and no name or identifier holds a control character.
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("'q' can't contain control characters.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListPeopleRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListPeopleRequest.Statuses)}.");
        RuleFor(request => request.AgeStatus)
            .Must(status => status is null || ListPeopleRequest.TryParseAgeStatus(status, out _))
            .WithMessage($"'ageStatus' must be one of: {string.Join(", ", ListPeopleRequest.AgeStatuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListPeopleRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListPeopleRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || PersonCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
