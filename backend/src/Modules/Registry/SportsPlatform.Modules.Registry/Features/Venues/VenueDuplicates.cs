using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary>The thresholds of the venue duplicate warning (S07 D13): constants, not tenant settings.</summary>
public static class VenueDuplicateThresholds
{
    /// <summary>The trigram similarity of two name keys from which they look alike (inclusive).</summary>
    public const double Similar = 0.7;

    /// <summary>The distance within which two venues look like one place, in metres (inclusive).</summary>
    public const double NearbyMetres = 200;

    public const int MaxCandidates = 10;
}

/// <summary>Why a venue looks like the one checked (S07 D13).</summary>
public enum VenueDuplicateEvidenceCode
{
    /// <summary>The same name key: the same words in any order, case, accents and punctuation aside.</summary>
    SameNameKey,

    /// <summary>Names at least <see cref="VenueDuplicateThresholds.Similar"/> similar; the value is the similarity.</summary>
    NameSimilarity,

    SameCity,

    /// <summary>Coordinates within <see cref="VenueDuplicateThresholds.NearbyMetres"/>; the value is the distance in metres.</summary>
    Nearby,
}

public sealed record VenueDuplicateEvidence(
    [property: PersonalData(PersonalDataClass.Public)] VenueDuplicateEvidenceCode Code,
    [property: PersonalData(PersonalDataClass.Public)] string? Value = null);

/// <summary>What the warning compares, normalised. <paramref name="VenueId"/> is the venue being changed, if any.</summary>
public sealed record VenueDuplicateInput(string NameKey, string CityKey, GeoPoint? Location, Guid? VenueId);

/// <summary>An existing venue that looks like the one checked, strongest first.</summary>
public sealed record VenueDuplicateCandidate(
    Guid VenueId,
    string Name,
    VenueType Type,
    string City,
    VenueStatus Status,
    DuplicateConfidence Confidence,
    IReadOnlyList<VenueDuplicateEvidence> Evidence,
    double Similarity);

/// <summary>
/// Finds the venues of the scope's tenant (query filter and RLS) that look like a <see cref="VenueDuplicateInput"/>
/// (S07 D13), archived ones included: <c>LIKELY</c> with the same name key and city, or coordinates within 200 m;
/// <c>POSSIBLE</c> with names at least 0.7 similar and the same city. A blank city never matches. Strongest first, then
/// the most similar name, then by name; at most <see cref="VenueDuplicateThresholds.MaxCandidates"/>.
/// </summary>
public sealed class VenueDuplicateFinder(RegistryDbContext context)
{
    // Rows read before classifying, in the query's order. Far above what one name or place finds.
    public const int MaxRows = 100;

    public async Task<IReadOnlyList<VenueDuplicateCandidate>> FindAsync(VenueDuplicateInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var key = input.NameKey;
        var cityKey = input.CityKey;
        var self = input.VenueId;
        var near = input.Location is { } location;
        var (latitude, longitude) = (input.Location?.Latitude ?? 0, input.Location?.Longitude ?? 0);
        var (latitudeSpan, longitudeSpan) = input.Location?.DegreesAround(VenueDuplicateThresholds.NearbyMetres) ?? (0, 0);
        var (minLatitude, maxLatitude) = (latitude - latitudeSpan, latitude + latitudeSpan);

        // A box that crosses the antimeridian (or spans every longitude near a pole) filters on latitude only.
        var (minLongitude, maxLongitude) = (longitude - longitudeSpan, longitude + longitudeSpan);
        var anyLongitude = minLongitude < -GeoPoint.MaxLongitude || maxLongitude > GeoPoint.MaxLongitude;

        var rows = await context.Set<Venue>().AsNoTracking()
            .Where(v => self == null || v.Id != self)
            .Where(v =>
                (cityKey != "" && v.CityKey == cityKey
                    && (v.NameKey == key || EF.Functions.TrigramsSimilarity(v.NameKey, key) >= VenueDuplicateThresholds.Similar - 0.0001))
                || (near && v.Latitude >= minLatitude && v.Latitude <= maxLatitude
                    && (anyLongitude || (v.Longitude >= minLongitude && v.Longitude <= maxLongitude))))
            .OrderByDescending(v => v.NameKey == key)
            .ThenByDescending(v => EF.Functions.TrigramsSimilarity(v.NameKey, key))
            .ThenBy(v => v.Id)
            .Take(MaxRows)
            .Select(v => new Row(
                v.Id, v.Name, v.Type, v.City, v.Status, v.NameKey == key, v.CityKey == cityKey,
                EF.Functions.TrigramsSimilarity(v.NameKey, key), v.Latitude, v.Longitude))
            .ToListAsync(cancellationToken);

        var candidates = new List<VenueDuplicateCandidate>();
        foreach (var row in rows)
        {
            if (Classify(input, row) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return
        [
            .. candidates
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.Similarity)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.VenueId)
                .Take(VenueDuplicateThresholds.MaxCandidates),
        ];
    }

    private static VenueDuplicateCandidate? Classify(VenueDuplicateInput input, Row row)
    {
        // pg_trgm answers a 4-byte float: 0.7 arrives as 0.699999988079071.
        var similarity = row.SameNameKey ? 1 : Math.Round(row.Similarity, 4);
        var sameCity = input.CityKey.Length > 0 && row.SameCity;
        double? distance = input.Location is { } location && row.Latitude is { } latitude && row.Longitude is { } longitude
            ? location.DistanceMetresTo(new GeoPoint(latitude, longitude))
            : null;
        var nearby = distance <= VenueDuplicateThresholds.NearbyMetres;

        var evidence = new List<VenueDuplicateEvidence>();
        if (sameCity && row.SameNameKey)
        {
            evidence.Add(new(VenueDuplicateEvidenceCode.SameNameKey));
        }
        else if (sameCity && similarity >= VenueDuplicateThresholds.Similar)
        {
            evidence.Add(new(VenueDuplicateEvidenceCode.NameSimilarity, similarity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (evidence.Count > 0)
        {
            evidence.Add(new(VenueDuplicateEvidenceCode.SameCity));
        }

        if (nearby)
        {
            evidence.Add(new(VenueDuplicateEvidenceCode.Nearby, Math.Round(distance!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (evidence.Count == 0)
        {
            return null;
        }

        var confidence = nearby || (sameCity && row.SameNameKey) ? DuplicateConfidence.Likely : DuplicateConfidence.Possible;
        return new VenueDuplicateCandidate(row.Id, row.Name, row.Type, row.City, row.Status, confidence, evidence, similarity);
    }

    private sealed record Row(
        Guid Id,
        string Name,
        VenueType Type,
        string City,
        VenueStatus Status,
        bool SameNameKey,
        bool SameCity,
        double Similarity,
        decimal? Latitude,
        decimal? Longitude);
}
