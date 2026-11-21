namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// A point on the earth in decimal degrees, to six places (about 0.1 m; <c>numeric(9,6)</c>, S07 D3). No geocoding
/// and no PostGIS: the duplicate check measures great-circle distance itself (<see cref="DistanceMetresTo"/>).
/// </summary>
public sealed record GeoPoint
{
    public const decimal MaxLatitude = 90m;
    public const decimal MaxLongitude = 180m;

    // The mean earth radius (IUGG), in metres.
    private const double _earthRadiusMetres = 6_371_008.8;

    public GeoPoint(decimal latitude, decimal longitude)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(latitude), MaxLatitude, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Abs(longitude), MaxLongitude, nameof(longitude));
        Latitude = Math.Round(latitude, 6, MidpointRounding.AwayFromZero);
        Longitude = Math.Round(longitude, 6, MidpointRounding.AwayFromZero);
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    /// <summary>The haversine distance to <paramref name="other"/>, in metres.</summary>
    public double DistanceMetresTo(GeoPoint other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(other.Longitude) - ToRadians(Longitude);
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * _earthRadiusMetres * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>
    /// How many degrees of latitude and longitude around this point hold every point within <paramref name="metres"/>
    /// (a box for an index-friendly first filter; the distance decides).
    /// </summary>
    public (decimal Latitude, decimal Longitude) DegreesAround(double metres)
    {
        var latitude = metres / _earthRadiusMetres * (180 / Math.PI);
        var cosine = Math.Cos(ToRadians(Latitude));

        // Near a pole every longitude is close: the whole range.
        var longitude = cosine < 1e-6 ? (double)MaxLongitude * 2 : latitude / cosine;
        return ((decimal)latitude * 1.01m, (decimal)Math.Min(longitude * 1.01, (double)MaxLongitude * 2));
    }

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180;
}
