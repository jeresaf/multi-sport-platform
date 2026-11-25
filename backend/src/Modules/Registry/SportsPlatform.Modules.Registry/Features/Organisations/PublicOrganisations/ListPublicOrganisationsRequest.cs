using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.PublicOrganisations;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; <paramref name="Status"/> is <c>ACTIVE</c> when none was sent (S07
/// D17); <paramref name="After"/> is the sort key and ID of the previous page's last organisation.
/// </summary>
public sealed record PublicOrganisationsQuery(
    string? Search, OrganisationType? Type, OrganisationStatus Status, int Limit, DirectoryCursor? After);

/// <summary>
/// The public directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>). The
/// rules are the admin directory's, but <c>status</c> takes every status except <c>ARCHIVED</c> (archived organisations
/// aren't public; owner, plan 2026-11-25).
/// </summary>
public sealed record ListPublicOrganisationsRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "type")] string? Type,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public static IReadOnlyCollection<string> Statuses { get; } =
        [.. ListOrganisationsRequest.Statuses.Where(status => TryParseStatus(status, out _))];

    public static bool TryParseStatus(string value, out OrganisationStatus status) =>
        ListOrganisationsRequest.TryParseStatus(value, out status) && status != OrganisationStatus.Archived;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public PublicOrganisationsQuery ToQuery()
    {
        // The admin request parses the shared parameters; the status is checked here.
        var shared = new ListOrganisationsRequest(Q, Type, Status: null, ParentId: null, Limit, Cursor).ToQuery();
        var status = OrganisationStatus.Active;
        if (Status is not null && !TryParseStatus(Status, out status))
        {
            throw new InvalidOperationException("Validate the request first.");
        }

        return new PublicOrganisationsQuery(shared.Search, shared.Type, status, shared.Limit, shared.After);
    }
}

/// <summary>The admin directory's rules, but <c>status</c> leaves out <c>ARCHIVED</c>. Messages never echo what was sent.</summary>
public sealed class ListPublicOrganisationsRequestValidator : AbstractValidator<ListPublicOrganisationsRequest>
{
    public ListPublicOrganisationsRequestValidator()
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
            .Must(status => status is null || ListPublicOrganisationsRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListPublicOrganisationsRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListOrganisationsRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListOrganisationsRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || ListOrganisationsRequest.TryParseCursor(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
