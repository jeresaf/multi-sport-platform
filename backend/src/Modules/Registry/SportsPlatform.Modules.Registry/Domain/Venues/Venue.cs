using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// A place where contests happen (<c>registry.venues</c>, tenant-owned, S07 D3): its address, time zone, capacity,
/// surfaces and the sport variants it can host. Its ID never changes (AR-23); its slug is unique in the tenant (D9).
/// The name and city are kept as entered and as normalised keys (<see cref="NameNormaliser"/>) for the duplicate check
/// (D13). The status history is its child: a new row changes the venue's version (S06 D11).
/// </summary>
public sealed class Venue : AggregateRoot, ITenantOwned
{
    private readonly List<VenueStatusChange> _statusHistory = [];
    private List<VenueSurface> _surfaces = [];
    private List<string> _supportedVariants = [];

    private Venue()
    {
    }

    private Venue(string slug, VenueSource source)
        : base(NewId())
    {
        Slug = slug;
        Source = source;
        Status = VenueStatus.Active;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? ShortName { get; private set; }

    /// <summary>The sorted normalised tokens of the name (S06 D4): what the duplicate check compares.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public VenueType Type { get; private set; }

    public string? AddressLine { get; private set; }

    public string City { get; private set; } = string.Empty;

    /// <summary>The normalised city: "the same city" in the duplicate check.</summary>
    public string CityKey { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    /// <summary>An active IANA zone: the local time of the venue's contests.</summary>
    public string TimeZone { get; private set; } = string.Empty;

    public int? Capacity { get; private set; }

    public IReadOnlyList<VenueSurface> Surfaces => _surfaces;

    /// <summary>Published variants the venue can host, not necessarily enabled for the tenant (S07 D3).</summary>
    public IReadOnlyList<string> SupportedVariants => _supportedVariants;

    public VenueStatus Status { get; private set; }

    public VenueSource Source { get; private set; }

    public IReadOnlyList<VenueStatusChange> StatusHistory => _statusHistory;

    /// <summary>Both coordinates, or null when the venue has none.</summary>
    public GeoPoint? Location => Latitude is { } latitude && Longitude is { } longitude ? new GeoPoint(latitude, longitude) : null;

    /// <summary>
    /// A new <c>ACTIVE</c> venue in the scope's tenant (stamped on save), with the first row of its status history. The
    /// use case has checked <paramref name="details"/>, chosen a free <paramref name="slug"/> and run the duplicate check.
    /// </summary>
    public static Venue Create(VenueDetails details, string slug, VenueSource source, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        if (!Slugs.IsValid(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        var venue = new Venue(slug, source);
        venue.Apply(details);
        venue._statusHistory.Add(new VenueStatusChange(venue.Id, null, venue.Status, null, actor, now));
        return venue;
    }

    /// <summary>The name key of a venue name; empty when it has no letter or digit.</summary>
    public static string NameKeyOf(string? name) => NameNormaliser.NameKey(name);

    /// <summary>The key "the same city" compares; empty for a blank city.</summary>
    public static string CityKeyOf(string? city) => NameNormaliser.Normalise(city);

    private void Apply(VenueDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.City);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Country);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.TimeZone);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(details.Name.Length, VenueDetails.NameMaxLength, nameof(details));
        if (details.Capacity is < 0 or > VenueDetails.MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "The capacity is out of bounds.");
        }

        if (details.Surfaces.Count > VenueDetails.MaxSurfaces || details.Surfaces.Distinct().Count() != details.Surfaces.Count)
        {
            throw new ArgumentException("At most five distinct surfaces.", nameof(details));
        }

        if (details.SupportedVariants.Count > VenueDetails.MaxSupportedVariants
            || details.SupportedVariants.Distinct(StringComparer.Ordinal).Count() != details.SupportedVariants.Count)
        {
            throw new ArgumentException("At most twenty distinct variants.", nameof(details));
        }

        var nameKey = NameKeyOf(details.Name);
        if (nameKey.Length == 0)
        {
            throw new ArgumentException("A venue's name needs a letter or a digit.", nameof(details));
        }

        Name = details.Name;
        ShortName = details.ShortName;
        NameKey = nameKey;
        Type = details.Type;
        AddressLine = details.AddressLine;
        City = details.City;
        CityKey = CityKeyOf(details.City);
        Region = details.Region;
        Country = details.Country;
        Latitude = details.Location?.Latitude;
        Longitude = details.Location?.Longitude;
        TimeZone = details.TimeZone;
        Capacity = details.Capacity;
        _surfaces = [.. details.Surfaces];
        _supportedVariants = [.. details.SupportedVariants];
    }
}
