using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// Venues (S07-T1a): the record's keys and first history row, slugs (D9), distances (D13), conflict codes (D2); S07-T1c:
/// aliases, the new-children guard and identifier codes (D10).
/// </summary>
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

    // S07-T1b-2: the directory's sort key keeps the word order the name key sorts.
    [Theory]
    [InlineData("Kiwatule Meadow Ground", "kiwatule meadow ground")]
    [InlineData("  ÉNSIBUKO   Grounds ", "ensibuko grounds")]
    [InlineData("Ggaba-Lakeside Rugby Ground", "ggabalakeside rugby ground")]
    [InlineData("St. Mbaaga's Field", "st mbaagas field")]
    public void The_sort_key_is_the_normalised_name_in_its_word_order(string name, string sortKey)
    {
        var venue = Venue.Create(Kiwatule() with { Name = name }, "ground", VenueSource.Manual, SystemActor.Instance, _now);
        venue.SortKey.ShouldBe(sortKey);

        venue.Update(Kiwatule(), "ground");
        venue.SortKey.ShouldBe("kiwatule meadow ground");
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

    // Review #118 R2: a venue loaded without its history is a bug, not a refused move.
    [Fact]
    public void A_move_without_the_loaded_history_throws()
    {
        var venue = Venue.Create(Kiwatule(), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);
        var history = (List<VenueStatusChange>)typeof(Venue)
            .GetField("_statusHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(venue)!;
        history.Clear();

        Should.Throw<InvalidOperationException>(() => venue.ChangeStatus(VenueStatus.Closed, "The ground was sold", SystemActor.Instance, _now));
        venue.Status.ShouldBe(VenueStatus.Active);
    }

    // S07-T1c (D10): an alias with its own key; a key the venue already has is refused; removal keeps who, when and why.
    [Fact]
    public void An_alias_has_its_own_key_and_is_refused_when_the_venue_is_already_known_by_it()
    {
        var venue = Venue.Create(Kiwatule(), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);

        var alias = venue.AddAlias("  The   Meadow ", AliasKind.Nickname).Value;

        (alias.VenueId, alias.Name, alias.NameKey, alias.Kind).ShouldBe((venue.Id, "The Meadow", "meadow the", AliasKind.Nickname));
        venue.AddAlias("Meadow Ground, Kiwatule", AliasKind.Spelling).Error.ShouldBe(RegistryErrors.VenueAliasExists);
        venue.AddAlias("the MEADOW", AliasKind.Spelling).Error.ShouldBe(RegistryErrors.VenueAliasExists);
        Should.Throw<ArgumentException>(() => venue.AddAlias("--", AliasKind.Spelling));

        venue.RemoveAlias(Guid.CreateVersion7(), "Misspelt", SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.VenueAliasNotFound);
        venue.RemoveAlias(alias.Id, " Misspelt ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        (alias.RemoveReason, alias.RemovedBy, alias.RemovedAt).ShouldBe(("Misspelt", SystemActor.Instance.ActorId, _now));
        venue.RemoveAlias(alias.Id, "Misspelt", SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.VenueAliasRemoved);

        // A removed alias's key is free again.
        venue.AddAlias("The Meadow", AliasKind.Nickname).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(VenueStatus.Active, null)]
    [InlineData(VenueStatus.TemporarilyUnavailable, null)]
    [InlineData(VenueStatus.Closed, "VENUE_CLOSED")]
    [InlineData(VenueStatus.Archived, "VENUE_ARCHIVED")]
    public void Only_a_venue_that_isnt_closed_or_archived_takes_new_children_but_every_one_removes_them(VenueStatus status, string? refusal)
    {
        var venue = Venue.Create(Kiwatule(), "kiwatule-meadow-ground", VenueSource.Manual, SystemActor.Instance, _now);
        var alias = venue.AddAlias("The Meadow", AliasKind.Nickname).Value;
        if (status != VenueStatus.Active)
        {
            venue.ChangeStatus(status, "Set up by the test", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        }

        venue.EnsureAcceptsNewChildren().IsSuccess.ShouldBe(refusal is null);
        var added = venue.AddAlias("Kiwatule Grounds", AliasKind.Spelling);
        (added.IsFailure ? added.Error.Code : null).ShouldBe(refusal);
        venue.RemoveAlias(alias.Id, "Misspelt", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    // S07 D10: ending an identifier answers in the area of what it names.
    [Fact]
    public void Ending_an_identifier_answers_with_its_entity_types_codes()
    {
        var today = new DateOnly(2026, 11, 21);
        var venueId = ExternalIdentifier.ForVenue(Guid.CreateVersion7(), "KCCA_FACILITY_NO", " KCCA-0147 ", today.AddDays(-5));
        var personId = ExternalIdentifier.ForPerson(Guid.CreateVersion7(), "KMSA_REG_NO", "KM-2026-0147", today.AddDays(-5));

        (venueId.EntityType, venueId.Value).ShouldBe((ExternalIdentifierEntityType.Venue, "KCCA-0147"));
        venueId.End(today.AddDays(1), today).Error.ShouldBe(RegistryErrors.VenueExternalIdValidityInvalid);
        venueId.End(today.AddDays(-6), today).Error.ShouldBe(RegistryErrors.VenueExternalIdValidityInvalid);
        personId.End(today.AddDays(1), today).Error.ShouldBe(RegistryErrors.ExternalIdValidityInvalid);
        venueId.End(today, today).IsSuccess.ShouldBeTrue();
        personId.End(today, today).IsSuccess.ShouldBeTrue();
        venueId.End(today, today).Error.ShouldBe(RegistryErrors.VenueExternalIdEnded);
        personId.End(today, today).Error.ShouldBe(RegistryErrors.ExternalIdEnded);
        Should.Throw<ArgumentException>(() => ExternalIdentifier.ForVenue(Guid.Empty, "KCCA_FACILITY_NO", "KCCA-0147"));
    }
}
