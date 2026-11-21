using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.Modules.Registry.Features.Venues.CheckVenueDuplicates;

/// <summary>
/// The live warning a venue form shows before it saves (S07 D13): the same comparison the create runs, without the
/// lock and without saving. A name without a letter or a digit is <c>VENUE_NAME_INVALID</c>.
/// </summary>
public sealed class CheckVenueDuplicatesHandler(VenueDuplicateFinder finder)
{
    public async Task<(CheckVenueDuplicatesResponse? Answer, FieldRefusal? Refusal)> HandleAsync(
        CheckVenueDuplicatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nameKey = Venue.NameKeyOf(request.Name);
        if (nameKey.Length == 0)
        {
            return (null, new(RegistryErrors.VenueNameInvalid, "name"));
        }

        var location = request.Latitude is { } latitude && request.Longitude is { } longitude ? new GeoPoint(latitude, longitude) : null;
        var candidates = await finder.FindAsync(
            new VenueDuplicateInput(nameKey, Venue.CityKeyOf(request.City), location, VenueId: null), cancellationToken);
        return (new CheckVenueDuplicatesResponse([.. candidates.Select(VenueDuplicateCandidateResponse.From)]), null);
    }
}
