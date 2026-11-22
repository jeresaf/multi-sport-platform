using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// Organisations (S07-T2a): the record's keys and first history row (D4, D13), the keys an optional name or city gives,
/// the warning's input, the conflict code (D2).
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
}
