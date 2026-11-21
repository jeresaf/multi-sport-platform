using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues.CheckVenueDuplicates;

/// <summary>What the venue duplicate warning compares (S07 D13): the name, the city and the coordinates a save would send.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckVenueDuplicatesRequest(string? Name, string? City, decimal? Latitude = null, decimal? Longitude = null);

/// <summary>The venues that look like the one checked, strongest first (none is an empty list).</summary>
public sealed record CheckVenueDuplicatesResponse(IReadOnlyList<VenueDuplicateCandidateResponse> Candidates);

/// <summary>The check's shape: the name and city as on a venue, both coordinates or neither, in range.</summary>
public sealed class CheckVenueDuplicatesRequestValidator : AbstractValidator<CheckVenueDuplicatesRequest>
{
    public CheckVenueDuplicatesRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("'name' must not be empty.")
            .Must(name => name!.Trim().Length <= VenueDetails.NameMaxLength)
            .WithMessage($"'name' must be at most {VenueDetails.NameMaxLength} characters.");
        RuleFor(request => request.City)
            .Cascade(CascadeMode.Stop)
            .Must(city => !string.IsNullOrWhiteSpace(city)).WithMessage("'city' must not be empty.")
            .Must(city => city!.Trim().Length <= VenueDetails.CityMaxLength)
            .WithMessage($"'city' must be at most {VenueDetails.CityMaxLength} characters.");
        RuleFor(request => request.Latitude)
            .Must(value => value is null || Math.Abs(value.Value) <= GeoPoint.MaxLatitude)
            .WithMessage($"'latitude' must be between -{GeoPoint.MaxLatitude} and {GeoPoint.MaxLatitude}.");
        RuleFor(request => request.Latitude)
            .NotNull().When(request => request.Longitude is not null)
            .WithMessage("'latitude' is required with 'longitude'.");
        RuleFor(request => request.Longitude)
            .Must(value => value is null || Math.Abs(value.Value) <= GeoPoint.MaxLongitude)
            .WithMessage($"'longitude' must be between -{GeoPoint.MaxLongitude} and {GeoPoint.MaxLongitude}.");
        RuleFor(request => request.Longitude)
            .NotNull().When(request => request.Latitude is not null)
            .WithMessage("'longitude' is required with 'latitude'.");
    }
}
