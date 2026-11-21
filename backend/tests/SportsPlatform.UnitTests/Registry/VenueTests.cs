using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Venues;
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

    // D13: the 200 m rule is inclusive; a venue without coordinates is never nearby.
    [Theory]
    [InlineData(200.0, true)]
    [InlineData(199.99, true)]
    [InlineData(200.01, false)]
    [InlineData(null, false)]
    public void Two_venues_are_nearby_up_to_200_m(double? metres, bool nearby) =>
        VenueDuplicateThresholds.IsNearby(metres).ShouldBe(nearby);

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

    // D12: every pair of statuses that isn't archived; ARCHIVED's own moves are below.
    public static TheoryData<VenueStatus, VenueStatus, bool> Moves() => new()
    {
        { VenueStatus.Active, VenueStatus.Active, false },
        { VenueStatus.Active, VenueStatus.TemporarilyUnavailable, true },
        { VenueStatus.Active, VenueStatus.Closed, true },
        { VenueStatus.Active, VenueStatus.Archived, true },
        { VenueStatus.TemporarilyUnavailable, VenueStatus.Active, true },
        { VenueStatus.TemporarilyUnavailable, VenueStatus.TemporarilyUnavailable, false },
        { VenueStatus.TemporarilyUnavailable, VenueStatus.Closed, true },
        { VenueStatus.TemporarilyUnavailable, VenueStatus.Archived, true },
        { VenueStatus.Closed, VenueStatus.Active, true },
        { VenueStatus.Closed, VenueStatus.TemporarilyUnavailable, false },
        { VenueStatus.Closed, VenueStatus.Closed, false },
        { VenueStatus.Closed, VenueStatus.Archived, true },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public void The_lifecycle_allows_only_its_moves(VenueStatus from, VenueStatus to, bool allowed) =>
        VenueTransitions.IsAllowed(from, to).ShouldBe(allowed);

    // Q55: unarchiving goes back to the status before archiving and nowhere else.
    [Theory]
    [InlineData(VenueStatus.Active)]
    [InlineData(VenueStatus.TemporarilyUnavailable)]
    [InlineData(VenueStatus.Closed)]
    public void An_archived_venue_goes_back_only_to_the_status_it_had(VenueStatus before)
    {
        foreach (var to in Enum.GetValues<VenueStatus>())
        {
            VenueTransitions.IsAllowed(VenueStatus.Archived, to, before).ShouldBe(to == before, $"{before} → ARCHIVED → {to}");
        }

        VenueTransitions.IsAllowed(VenueStatus.Archived, VenueStatus.Active).ShouldBeFalse();
    }

    [Fact]
    public void Each_move_writes_its_history_row_and_unarchiving_reads_the_latest_archive()
    {
        var venue = Venue.Create(Kiwatule(), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);

        venue.ChangeStatus(VenueStatus.Archived, " Recorded twice ", SystemActor.Instance, _now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        venue.StatusBeforeArchive().ShouldBe(VenueStatus.Active);
        venue.ChangeStatus(VenueStatus.Active, "Recorded once after all", SystemActor.Instance, _now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        venue.ChangeStatus(VenueStatus.Closed, "The ground was sold", SystemActor.Instance, _now.AddMinutes(3)).IsSuccess.ShouldBeTrue();
        venue.ChangeStatus(VenueStatus.Archived, "No longer used", SystemActor.Instance, _now.AddMinutes(4)).IsSuccess.ShouldBeTrue();

        venue.StatusBeforeArchive().ShouldBe(VenueStatus.Closed);
        venue.ChangeStatus(VenueStatus.Active, "Back in use", SystemActor.Instance, _now.AddMinutes(5)).Error!.Code.ShouldBe("VENUE_STATUS_INVALID");
        venue.ChangeStatus(VenueStatus.Closed, "Archived by mistake", SystemActor.Instance, _now.AddMinutes(5)).IsSuccess.ShouldBeTrue();

        venue.Status.ShouldBe(VenueStatus.Closed);
        venue.StatusBeforeArchive().ShouldBeNull();
        venue.StatusHistory.Select(c => (c.FromStatus, c.ToStatus)).ShouldBe(
        [
            (null, VenueStatus.Active),
            (VenueStatus.Active, VenueStatus.Archived),
            (VenueStatus.Archived, VenueStatus.Active),
            (VenueStatus.Active, VenueStatus.Closed),
            (VenueStatus.Closed, VenueStatus.Archived),
            (VenueStatus.Archived, VenueStatus.Closed),
        ]);
        venue.StatusHistory[1].Reason.ShouldBe("Recorded twice");
    }

    [Fact]
    public void An_update_replaces_the_fields_and_keys_and_says_when_the_compared_fields_change()
    {
        var venue = Venue.Create(Kiwatule(new GeoPoint(0.371245m, 32.618310m)), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);

        // Case, punctuation and word order aside, and the same point to six places: nothing the warning compares.
        venue.ChangesComparedFields(Kiwatule(new GeoPoint(0.3712450m, 32.61831m)) with { Name = "Ground, KIWATULE Meadow", Capacity = 900 }).ShouldBeFalse();
        venue.ChangesComparedFields(Kiwatule(new GeoPoint(0.371245m, 32.618310m)) with { Name = "Kiwatule Meadow Grounds" }).ShouldBeTrue();
        venue.ChangesComparedFields(Kiwatule(new GeoPoint(0.371245m, 32.618310m)) with { City = "Wakiso" }).ShouldBeTrue();
        venue.ChangesComparedFields(Kiwatule(new GeoPoint(0.371246m, 32.618310m))).ShouldBeTrue();
        venue.ChangesComparedFields(Kiwatule()).ShouldBeTrue();

        venue.Update(Kiwatule() with { Name = "Kiwatule Recreation Ground", City = "Wakiso", ShortName = null, Surfaces = [] }, "kiwatule-recreation");

        (venue.Name, venue.NameKey, venue.City, venue.CityKey, venue.Slug).ShouldBe(
            ("Kiwatule Recreation Ground", "ground kiwatule recreation", "Wakiso", "wakiso", "kiwatule-recreation"));
        (venue.ShortName, venue.Latitude, venue.Longitude, venue.Surfaces.Count).ShouldBe((null, null, null, 0));
        (venue.Status, venue.StatusHistory.Count).ShouldBe((VenueStatus.Active, 1));
        Should.Throw<ArgumentException>(() => venue.Update(Kiwatule(), "Not A Slug"));
    }
}
