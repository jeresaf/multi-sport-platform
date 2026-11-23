using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People.Profiles;

namespace SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListVenuesRequestValidator : AbstractValidator<ListVenuesRequest>
{
    public ListVenuesRequestValidator()
    {
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length <= ListVenuesRequest.SearchMaxLength)
            .WithMessage($"'q' is at most {ListVenuesRequest.SearchMaxLength} characters.")
            // PostgreSQL refuses a NUL in text, and no name holds a control character.
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("'q' can't contain control characters.");
        RuleFor(request => request.City)
            .Must(city => city is null || city.Length <= VenueDetails.CityMaxLength)
            .WithMessage($"'city' is at most {VenueDetails.CityMaxLength} characters.")
            .Must(city => city is null || !city.Any(char.IsControl))
            .WithMessage("'city' can't contain control characters.");
        RuleFor(request => request.Variant)
            .Must(variant => variant is null || AthleteProfileDetailsRules.IsCode(variant, VenueDetails.VariantMaxLength))
            .WithMessage($"'variant' must be a sport variant code of at most {VenueDetails.VariantMaxLength} characters.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListVenuesRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListVenuesRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListVenuesRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListVenuesRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || DirectoryCursor.TryDecode(cursor, VenueDetails.NameMaxLength, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
