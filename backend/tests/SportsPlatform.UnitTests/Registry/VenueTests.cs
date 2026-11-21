using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>Venues (S07-T1a): the record's keys and first history row, slugs (D9), distances (D13), conflict codes (D2).</summary>
public sealed class VenueTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private static VenueDetails Kiwatule(GeoPoint? location = null) =>
        new("Kiwatule Meadow Ground", "Kiwatule Meadow", VenueType.SportsGround, null, "Kampala", "Central", "UG", location,
            "Africa/Kampala", 1_200, [VenueSurface.NaturalGrass], ["TESTBALL_FULL"]);

    [Fact]
    public void A_new_venue_is_active_with_its_keys_and_the_first_row_of_its_history()
    {
        var venue = Venue.Create(Kiwatule(new GeoPoint(0.3712454m, 32.6183104m)), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);

        (venue.Status, venue.NameKey, venue.CityKey, venue.Slug).ShouldBe((VenueStatus.Active, "ground kiwatule meadow", "kampala", "kiwatule-meadow-ground"));
        (venue.Latitude, venue.Longitude).ShouldBe((0.371245m, 32.618310m));
        var first = venue.StatusHistory.ShouldHaveSingleItem();
        (first.VenueId, first.FromStatus, first.ToStatus, first.OccurredAt).ShouldBe((venue.Id, null, VenueStatus.Active, _now));
    }

    [Fact]
    public void A_venue_refuses_a_name_without_letters_a_bad_slug_and_repeated_surfaces()
    {
        Should.Throw<ArgumentException>(() => Venue.Create(Kiwatule() with { Name = "--" }, "ground", VenueSource.Manual, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => Venue.Create(Kiwatule(), "Kiwatule", VenueSource.Manual, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => Venue.Create(
            Kiwatule() with { Surfaces = [VenueSurface.Clay, VenueSurface.Clay] }, "ground", VenueSource.Manual, SystemActor.Instance, _now));
        Should.Throw<ArgumentOutOfRangeException>(() => Venue.Create(
            Kiwatule() with { Capacity = 200_001 }, "ground", VenueSource.Manual, SystemActor.Instance, _now));
    }

    [Theory]
    [InlineData("Kiwatule Meadow Ground", "kiwatule-meadow-ground")]
    [InlineData("  Métro Indoor Arena (Naguru) ", "metro-indoor-arena-naguru")]
    [InlineData("Ntinda Swifts' Ground — East", "ntinda-swifts-ground-east")]
    [InlineData("Ground 2", "ground-2")]
    [InlineData("—", "venue")]
    [InlineData("A", "venue")]
    [InlineData("0192a1b2-0000-7000-8000-00000000c001", "venue")]
    public void A_slug_is_proposed_from_the_name(string name, string slug) =>
        Slugs.Propose("venue", name).ShouldBe(slug);

    [Fact]
    public void A_long_name_leaves_room_for_a_suffix()
    {
        var slug = Slugs.Propose("venue", string.Join(' ', Enumerable.Repeat("Kisaasi", 20)));

        slug.Length.ShouldBeLessThanOrEqualTo(Slugs.MaxLength - 7);
        Slugs.IsValid(Slugs.WithSuffix(slug, 999_999)).ShouldBeTrue();
        Slugs.WithSuffix("kisaasi", 1).ShouldBe("kisaasi");
        Slugs.WithSuffix("kisaasi", 3).ShouldBe("kisaasi-3");
    }

    [Theory]
    [InlineData("kisaasi", true)]
    [InlineData("k2", true)]
    [InlineData("k", false)]
    [InlineData("kisaasi-", false)]
    [InlineData("kisaasi--ground", false)]
    [InlineData("Kisaasi", false)]
    [InlineData("kisaasi_ground", false)]
    [InlineData("0192a1b2-0000-7000-8000-00000000c001", false)]
    public void A_slug_is_lower_case_words_joined_by_hyphens_and_never_a_uuid(string slug, bool valid) =>
        Slugs.IsValid(slug).ShouldBe(valid);

    // The duplicate warning's edge (D13): 0.001790° of latitude is 199 m, 0.001808° is 201 m.
    [Theory]
    [InlineData(0.351790, 32.600000, 199.0)]
    [InlineData(0.351808, 32.600000, 201.0)]
    [InlineData(0.350000, 32.601798, 200.0)]
    public void The_distance_between_two_points_is_the_great_circle_in_metres(double latitude, double longitude, double metres)
    {
        var from = new GeoPoint(0.350000m, 32.600000m);

        from.DistanceMetresTo(new GeoPoint((decimal)latitude, (decimal)longitude)).ShouldBe(metres, tolerance: 0.5);
    }

    [Fact]
    public void The_box_around_a_point_holds_every_point_within_the_distance()
    {
        var from = new GeoPoint(0.350000m, 32.600000m);

        var (latitude, longitude) = from.DegreesAround(200);

        from.DistanceMetresTo(new GeoPoint(0.350000m + latitude, 32.600000m)).ShouldBeGreaterThan(200);
        from.DistanceMetresTo(new GeoPoint(0.350000m, 32.600000m + longitude)).ShouldBeGreaterThan(200);
    }

    // D2: each aggregate root answers its own conflict; a row of no root falls back to the module's.
    [Fact]
    public void Each_root_has_its_own_conflict_code()
    {
        RegistryConcurrencyConflicts.For(typeof(Venue))!.Code.ShouldBe("VENUE_CONCURRENCY_CONFLICT");
        RegistryConcurrencyConflicts.For(typeof(Person))!.Code.ShouldBe("PERSON_CONCURRENCY_CONFLICT");
        RegistryConcurrencyConflicts.For(typeof(PersonAlias)).ShouldBeNull();
    }
}
