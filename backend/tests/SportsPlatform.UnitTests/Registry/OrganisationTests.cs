using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// Organisations (S07-T2a, T2b-1): the record's keys and first history row (D4, D13), the keys an optional name or city
/// gives, the warning's input, the conflict code (D2), the lifecycle (D12, Q55) and the update (D11).
/// </summary>
public sealed class OrganisationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static OrganisationDetails NtindaSwifts() =>
        new(OrganisationType.Club, "Ntinda Swifts SC", "Ntinda Swifts Sports Club Limited", "Swifts", "UG", "Kampala", 1998,
            "https://ntindaswifts.example", "office@ntindaswifts.example", "+256 414 220 310", null, null);

    [Fact]
    public void A_new_organisation_is_active_with_its_keys_and_the_first_row_of_its_history()
    {
        var organisation = Organisation.Create(NtindaSwifts(), "ntinda-swifts-sc", OrganisationSource.Manual, SystemActor.Instance, _now);

        (organisation.Status, organisation.Slug, organisation.Type).ShouldBe((OrganisationStatus.Active, "ntinda-swifts-sc", OrganisationType.Club));
        (organisation.DisplayNameKey, organisation.LegalNameKey, organisation.ShortNameKey, organisation.SortKey, organisation.CityKey)
            .ShouldBe(("ntinda sc swifts", "club limited ntinda sports swifts", "swifts", "ntinda swifts sc", "kampala"));
        organisation.LogoAssetId.ShouldBeNull();
        var first = organisation.StatusHistory.ShouldHaveSingleItem();
        (first.OrganisationId, first.FromStatus, first.ToStatus, first.Reason, first.OccurredAt)
            .ShouldBe((organisation.Id, null, OrganisationStatus.Active, null, _now));
    }

    // D13: an optional name or a city without a letter or digit has no key, so it never matches.
    [Fact]
    public void Names_and_cities_without_letters_or_digits_have_no_key()
    {
        var organisation = Organisation.Create(
            NtindaSwifts() with { LegalName = "--", ShortName = null, City = "..." }, "ntinda-swifts-sc", OrganisationSource.Manual,
            SystemActor.Instance, _now);

        (organisation.LegalNameKey, organisation.ShortNameKey, organisation.CityKey).ShouldBe((null, null, null));
        (organisation.LegalName, organisation.City).ShouldBe(("--", "..."));
        Organisation.CityKeyOf(null).ShouldBeNull();
        Organisation.OptionalNameKeyOf("  ").ShouldBeNull();
    }

    [Fact]
    public void An_organisation_refuses_a_display_name_without_letters_a_bad_slug_and_an_early_year()
    {
        Should.Throw<ArgumentException>(() => Organisation.Create(
            NtindaSwifts() with { DisplayName = "--" }, "ntinda", OrganisationSource.Manual, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => Organisation.Create(
            NtindaSwifts(), "Ntinda Swifts", OrganisationSource.Manual, SystemActor.Instance, _now));
        Should.Throw<ArgumentOutOfRangeException>(() => Organisation.Create(
            NtindaSwifts() with { FoundedYear = 1799 }, "ntinda", OrganisationSource.Manual, SystemActor.Instance, _now));
    }

    [Fact]
    public void The_warnings_input_has_every_name_key_once()
    {
        var input = CreateOrganisationHandler.InputOf(
            NtindaSwifts() with { ShortName = "SC Swifts, Ntinda" }, organisationId: null);

        input.NameKeys.ShouldBe(["ntinda sc swifts", "club limited ntinda sports swifts"]);
        (input.Type, input.CityKey).ShouldBe((OrganisationType.Club, "kampala"));
    }

    [Fact]
    public void An_organisation_has_its_own_conflict_code()
    {
        RegistryConcurrencyConflicts.For(typeof(Organisation))!.Code.ShouldBe("ORGANISATION_CONCURRENCY_CONFLICT");
        RegistryConcurrencyConflicts.For(typeof(OrganisationStatusChange)).ShouldBeNull();
    }

    [Fact]
    public void The_thresholds_are_the_specs()
    {
        (OrganisationDuplicateThresholds.Similar, OrganisationDuplicateThresholds.MaxCandidates).ShouldBe((0.7, 10));
    }

    private static readonly OrganisationStatus[] _open =
        [OrganisationStatus.Active, OrganisationStatus.Inactive, OrganisationStatus.Suspended];

    // D12: ACTIVE, INACTIVE and SUSPENDED move between each other and to DISSOLVED or ARCHIVED; DISSOLVED only to ARCHIVED.
    public static TheoryData<OrganisationStatus, OrganisationStatus, bool> Moves()
    {
        var data = new TheoryData<OrganisationStatus, OrganisationStatus, bool>();
        foreach (var from in Enum.GetValues<OrganisationStatus>().Where(status => status != OrganisationStatus.Archived))
        {
            foreach (var to in Enum.GetValues<OrganisationStatus>())
            {
                var allowed = from == OrganisationStatus.Dissolved
                    ? to == OrganisationStatus.Archived
                    : from != to;
                data.Add(from, to, allowed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Moves))]
    public void The_lifecycle_allows_only_its_moves(OrganisationStatus from, OrganisationStatus to, bool allowed) =>
        OrganisationTransitions.IsAllowed(from, to).ShouldBe(allowed);

    [Fact]
    public void The_moves_are_the_specs_table()
    {
        // Spelled out once, so the generated table above can't drift from D12 unnoticed.
        foreach (var from in _open)
        {
            OrganisationTransitions.IsAllowed(from, OrganisationStatus.Dissolved).ShouldBeTrue();
            OrganisationTransitions.IsAllowed(from, OrganisationStatus.Archived).ShouldBeTrue();
        }

        OrganisationTransitions.IsAllowed(OrganisationStatus.Dissolved, OrganisationStatus.Active).ShouldBeFalse();
        OrganisationTransitions.IsAllowed(OrganisationStatus.Dissolved, OrganisationStatus.Inactive).ShouldBeFalse();
        OrganisationTransitions.IsAllowed(OrganisationStatus.Suspended, OrganisationStatus.Inactive).ShouldBeTrue();
    }

    // Q55: unarchiving goes back to the status before archiving and nowhere else.
    [Theory]
    [InlineData(OrganisationStatus.Active)]
    [InlineData(OrganisationStatus.Inactive)]
    [InlineData(OrganisationStatus.Suspended)]
    [InlineData(OrganisationStatus.Dissolved)]
    public void An_archived_organisation_goes_back_only_to_the_status_it_had(OrganisationStatus before)
    {
        foreach (var to in Enum.GetValues<OrganisationStatus>())
        {
            OrganisationTransitions.IsAllowed(OrganisationStatus.Archived, to, before)
                .ShouldBe(to == before, $"{before} → ARCHIVED → {to}");
        }

        OrganisationTransitions.IsAllowed(OrganisationStatus.Archived, OrganisationStatus.Active).ShouldBeFalse();
    }

    [Fact]
    public void A_dissolved_organisation_archived_and_unarchived_stays_dissolved()
    {
        var organisation = Organisation.Create(NtindaSwifts(), "ntinda-swifts-sc", OrganisationSource.Manual, SystemActor.Instance, _now);

        organisation.ChangeStatus(OrganisationStatus.Dissolved, " Merged into another club ", SystemActor.Instance, _now.AddMinutes(1))
            .IsSuccess.ShouldBeTrue();
        organisation.ChangeStatus(OrganisationStatus.Archived, "No longer listed", SystemActor.Instance, _now.AddMinutes(2))
            .IsSuccess.ShouldBeTrue();
        organisation.StatusBeforeArchive().ShouldBe(OrganisationStatus.Dissolved);
        organisation.ChangeStatus(OrganisationStatus.Active, "Archived by mistake", SystemActor.Instance, _now.AddMinutes(3))
            .Error!.Code.ShouldBe("ORGANISATION_STATUS_INVALID");
        organisation.ChangeStatus(OrganisationStatus.Dissolved, "Archived by mistake", SystemActor.Instance, _now.AddMinutes(3))
            .IsSuccess.ShouldBeTrue();

        organisation.Status.ShouldBe(OrganisationStatus.Dissolved);
        organisation.StatusBeforeArchive().ShouldBeNull();
        organisation.StatusHistory.Select(c => (c.FromStatus, c.ToStatus)).ShouldBe(
        [
            (null, OrganisationStatus.Active),
            (OrganisationStatus.Active, OrganisationStatus.Dissolved),
            (OrganisationStatus.Dissolved, OrganisationStatus.Archived),
            (OrganisationStatus.Archived, OrganisationStatus.Dissolved),
        ]);
        organisation.StatusHistory[1].Reason.ShouldBe("Merged into another club");
    }

    [Fact]
    public void A_move_without_the_loaded_history_throws()
    {
        var organisation = Organisation.Create(NtindaSwifts(), "ntinda-swifts-sc", OrganisationSource.Manual, SystemActor.Instance, _now);
        var history = (List<OrganisationStatusChange>)typeof(Organisation)
            .GetField("_statusHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(organisation)!;
        history.Clear();

        Should.Throw<InvalidOperationException>(() =>
            organisation.ChangeStatus(OrganisationStatus.Suspended, "Fees unpaid", SystemActor.Instance, _now));
        organisation.Status.ShouldBe(OrganisationStatus.Active);
    }

    [Fact]
    public void An_update_replaces_the_fields_and_keys_and_says_when_the_compared_fields_change()
    {
        var organisation = Organisation.Create(NtindaSwifts(), "ntinda-swifts-sc", OrganisationSource.Manual, SystemActor.Instance, _now);

        // Case, punctuation and word order aside, and fields the warning doesn't compare: no change.
        organisation.ChangesComparedFields(NtindaSwifts() with
        {
            DisplayName = "SC, NTINDA Swifts",
            LegalName = "ntinda swifts sports club limited",
            City = " kampala ",
            FoundedYear = 1999,
            Website = null,
            Description = "A multi-sport club.",
        }).ShouldBeFalse();
        organisation.ChangesComparedFields(NtindaSwifts() with { DisplayName = "Ntinda Swifts FC" }).ShouldBeTrue();
        organisation.ChangesComparedFields(NtindaSwifts() with { LegalName = null }).ShouldBeTrue();
        organisation.ChangesComparedFields(NtindaSwifts() with { ShortName = "Ntinda" }).ShouldBeTrue();
        organisation.ChangesComparedFields(NtindaSwifts() with { Type = OrganisationType.School }).ShouldBeTrue();
        organisation.ChangesComparedFields(NtindaSwifts() with { City = "Wakiso" }).ShouldBeTrue();
        organisation.ChangesComparedFields(NtindaSwifts() with { City = null }).ShouldBeTrue();

        organisation.Update(NtindaSwifts() with { DisplayName = "Ntinda Swifts FC", LegalName = null, City = "Wakiso" }, "ntinda-swifts");

        (organisation.DisplayName, organisation.DisplayNameKey, organisation.SortKey, organisation.LegalNameKey, organisation.CityKey, organisation.Slug)
            .ShouldBe(("Ntinda Swifts FC", "fc ntinda swifts", "ntinda swifts fc", null, "wakiso", "ntinda-swifts"));
        (organisation.Status, organisation.StatusHistory.Count).ShouldBe((OrganisationStatus.Active, 1));
        Should.Throw<ArgumentException>(() => organisation.Update(NtindaSwifts(), "Not A Slug"));
    }
}
