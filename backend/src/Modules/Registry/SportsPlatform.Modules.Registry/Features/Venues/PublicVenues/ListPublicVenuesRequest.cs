using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People.Profiles;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

namespace SportsPlatform.Modules.Registry.Features.Venues.PublicVenues;

/// <summary>
/// <paramref name="Search"/> is the trimmed <c>q</c>; <paramref name="Status"/> is <c>ACTIVE</c> when none was sent (S07
/// D17); <paramref name="After"/> is the sort key and ID of the previous page's last venue.
/// </summary>
public sealed record PublicVenuesQuery(string? Search, string? Variant, VenueStatus Status, int Limit, DirectoryCursor? After);

/// <summary>
/// The public directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>): the
/// admin directory's rules without <c>city</c> (D17), and <c>status</c> takes every status except <c>ARCHIVED</c>
/// (owner, plan 2026-11-25).
/// </summary>
public sealed record ListPublicVenuesRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "variant")] string? Variant,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public static IReadOnlyCollection<string> Statuses { get; } =
        [.. ListVenuesRequest.Statuses.Where(status => TryParseStatus(status, out _))];

    public static bool TryParseStatus(string value, out VenueStatus status) =>
        ListVenuesRequest.TryParseStatus(value, out status) && status != VenueStatus.Archived;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public PublicVenuesQuery ToQuery()
    {
        // The admin request parses the shared parameters; the status is checked here.
        var shared = new ListVenuesRequest(Q, Variant, City: null, Status: null, Limit, Cursor).ToQuery();
        var status = VenueStatus.Active;
        if (Status is not null && !TryParseStatus(Status, out status))
        {
            throw new InvalidOperationException("Validate the request first.");
        }

        return new PublicVenuesQuery(shared.Search, shared.Variant, status, shared.Limit, shared.After);
    }
}

/// <summary>The admin directory's rules, but <c>status</c> leaves out <c>ARCHIVED</c>. Messages never echo what was sent.</summary>
public sealed class ListPublicVenuesRequestValidator : AbstractValidator<ListPublicVenuesRequest>
{
    public ListPublicVenuesRequestValidator()
    {
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length <= ListVenuesRequest.SearchMaxLength)
            .WithMessage($"'q' is at most {ListVenuesRequest.SearchMaxLength} characters.")
            // PostgreSQL refuses a NUL in text, and no name holds a control character.
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("'q' can't contain control characters.");
        RuleFor(request => request.Variant)
            .Must(variant => variant is null || AthleteProfileDetailsRules.IsCode(variant, VenueDetails.VariantMaxLength))
            .WithMessage($"'variant' must be a sport variant code of at most {VenueDetails.VariantMaxLength} characters.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListPublicVenuesRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListPublicVenuesRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListVenuesRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListVenuesRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || DirectoryCursor.TryDecode(cursor, VenueDetails.NameMaxLength, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
