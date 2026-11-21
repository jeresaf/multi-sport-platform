using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary>A venue's editable fields as sent (S07 D3), shared by create and (S07-T1b) update.</summary>
public interface IVenueDetailsRequest
{
    string? Name { get; }

    string? ShortName { get; }

    VenueType? Type { get; }

    string? AddressLine { get; }

    string? City { get; }

    string? Region { get; }

    string? Country { get; }

    decimal? Latitude { get; }

    decimal? Longitude { get; }

    string? TimeZone { get; }

    int? Capacity { get; }

    IReadOnlyList<VenueSurface>? Surfaces { get; }

    IReadOnlyList<string?>? SupportedVariants { get; }
}

/// <summary>
/// The shape of <see cref="IVenueDetailsRequest"/> (S07 D3). Messages name the field, never echo what was sent. The zone
/// and the country are active ones of the reference data. Whether the name has a letter or a digit and whether the
/// variants are published are <see cref="VenueDetailsRules"/> (422s with their own codes). Scoped: it reads the
/// reference data.
/// </summary>
public sealed class VenueDetailsValidator<T> : AbstractValidator<T>
    where T : IVenueDetailsRequest
{
    public VenueDetailsValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        Text(RuleFor(request => request.Name), "name", VenueDetails.NameMaxLength, required: true);
        Text(RuleFor(request => request.ShortName), "shortName", VenueDetails.ShortNameMaxLength, required: false);
        Text(RuleFor(request => request.AddressLine), "addressLine", VenueDetails.AddressLineMaxLength, required: false);
        Text(RuleFor(request => request.City), "city", VenueDetails.CityMaxLength, required: true);
        Text(RuleFor(request => request.Region), "region", VenueDetails.RegionMaxLength, required: false);
        RuleFor(request => request.Type).NotNull().WithMessage("'type' is required.");
        RuleFor(request => request.Country)
            .Cascade(CascadeMode.Stop)
            .Must(code => code is null || (code.Length == 2 && code.All(char.IsAsciiLetterUpper)))
            .WithMessage("'country' must be an ISO 3166-1 alpha-2 country code in upper case.")
            .MustAsync(async (code, cancellationToken) => code is null || await reference.IsActiveCountryAsync(code, cancellationToken))
            .WithMessage("'country' must be a country of the reference data.");
        RuleFor(request => request.TimeZone)
            .Cascade(CascadeMode.Stop)
            .Must(zone => !string.IsNullOrWhiteSpace(zone))
            .WithMessage("'timeZone' is required.")
            .Must(zone => zone!.Length <= VenueDetails.TimeZoneMaxLength)
            .WithMessage("'timeZone' must be an IANA time zone.")
            .MustAsync((zone, cancellationToken) => reference.IsActiveTimeZoneAsync(zone!, cancellationToken))
            .WithMessage("'timeZone' must be an active time zone of the reference data.");
        Coordinate(RuleFor(request => request.Latitude), "latitude", GeoPoint.MaxLatitude);
        Coordinate(RuleFor(request => request.Longitude), "longitude", GeoPoint.MaxLongitude);
        RuleFor(request => request.Longitude)
            .NotNull().When(request => request.Latitude is not null)
            .WithMessage("'longitude' is required with 'latitude'.");
        RuleFor(request => request.Latitude)
            .NotNull().When(request => request.Longitude is not null)
            .WithMessage("'latitude' is required with 'longitude'.");
        RuleFor(request => request.Capacity)
            .InclusiveBetween(0, VenueDetails.MaxCapacity)
            .WithMessage($"'capacity' must be between 0 and {VenueDetails.MaxCapacity}.");
        RuleFor(request => request.Surfaces)
            .Must(surfaces => surfaces is null || surfaces.Count <= VenueDetails.MaxSurfaces)
            .WithMessage($"'surfaces' has at most {VenueDetails.MaxSurfaces} entries.")
            .Must(surfaces => surfaces is null || surfaces.Distinct().Count() == surfaces.Count)
            .WithMessage("'surfaces' names a surface more than once.");
        RuleFor(request => request.SupportedVariants)
            .Must(variants => variants is null || variants.Count <= VenueDetails.MaxSupportedVariants)
            .WithMessage($"'supportedVariants' has at most {VenueDetails.MaxSupportedVariants} entries.")
            .Must(variants => variants is null || variants.Distinct(StringComparer.Ordinal).Count() == variants.Count)
            .WithMessage("'supportedVariants' names a variant more than once.");
        RuleForEach(request => request.SupportedVariants)
            .Must(variant => !string.IsNullOrWhiteSpace(variant) && variant.Length <= VenueDetails.VariantMaxLength)
            .WithMessage("An entry of 'supportedVariants' must be a sport variant's code.");
    }

    private static void Text(IRuleBuilderInitial<T, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(text => !required || !string.IsNullOrWhiteSpace(text))
        .WithMessage($"'{field}' must not be empty.")
        .Must(text => text is null || text.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");

    private static void Coordinate(IRuleBuilderInitial<T, decimal?> rule, string field, decimal max) => rule
        .Must(value => value is null || (Math.Abs(value.Value) <= max && decimal.Round(value.Value, 6) == value.Value))
        .WithMessage($"'{field}' must be between -{max} and {max}, with at most six decimal places.");
}

/// <summary>
/// The rules on venue details that need more than the request's shape, with their own codes (422): a name without a
/// letter or a digit has no name key (<c>VENUE_NAME_INVALID</c>); a supported variant must have a published definition,
/// enabled for the tenant or not (<c>VENUE_SPORT_UNKNOWN</c>, S07 D3). Never names a sport (AR-11).
/// </summary>
public sealed class VenueDetailsRules(ISportDefinitions definitions)
{
    /// <summary>
    /// The details to save, trimmed, with <paramref name="defaultCountry"/> when none was sent; or the first refusal.
    /// </summary>
    public async Task<(VenueDetails? Details, FieldRefusal? Refusal)> CheckAsync(
        IVenueDetailsRequest request, string defaultCountry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Venue.NameKeyOf(request.Name).Length == 0)
        {
            return (null, new(RegistryErrors.VenueNameInvalid, "name"));
        }

        var variants = request.SupportedVariants ?? [];
        if (variants.Count > 0)
        {
            var published = (await definitions.ListAsync(cancellationToken))
                .Select(variant => variant.Variant)
                .ToHashSet(StringComparer.Ordinal);
            for (var i = 0; i < variants.Count; i++)
            {
                if (!published.Contains(variants[i]!))
                {
                    return (null, new(RegistryErrors.VenueSportUnknown, $"supportedVariants[{i}]"));
                }
            }
        }

        var location = request.Latitude is { } latitude && request.Longitude is { } longitude ? new GeoPoint(latitude, longitude) : null;
        return (new VenueDetails(
            request.Name!.Trim(),
            Blank(request.ShortName),
            request.Type!.Value,
            Blank(request.AddressLine),
            request.City!.Trim(),
            Blank(request.Region),
            request.Country ?? defaultCountry,
            location,
            request.TimeZone!,
            request.Capacity,
            [.. request.Surfaces ?? []],
            [.. variants.Select(variant => variant!)]), null);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
