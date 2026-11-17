using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.UnitTests.Registry;

// S06 D12 (owner, S06-T4b2 plan): an official profile on a person, its capabilities replaced as a whole, its moves and
// its licence's expiry on the tenant's date. The codes are test codes: the domain never reads them (AR-11).
public sealed class OfficialProfileTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly OfficialCapabilityDetails _referee =
        new("TESTBALL_FULL", "TB_REFEREE", " Class   1 ", " UG-REF  0147 ", new DateOnly(2027, 6, 30));

    private static readonly OfficialCapabilityDetails _assistant = new("TESTBALL_FULL", "TB_ASSISTANT", null, null, null);
    private static readonly OfficialCapabilityDetails _hoopReferee = new("TESTHOOP_FULL", "TH_REFEREE", null, null, null);

    [Fact]
    public void A_new_profile_is_active_with_its_capabilities_and_its_first_history_row()
    {
        var person = NewPerson();

        var profile = person.AddOfficialProfile([_referee, _hoopReferee], SystemActor.Instance, _now).Value;

        (profile.PersonId, profile.Status).ShouldBe((person.Id, ProfileStatus.Active));
        profile.Capabilities.Select(c => (c.OfficialProfileId, c.Variant, c.Role, c.Grade, c.LicenceNumber, c.LicenceValidUntil)).ShouldBe(
        [
            (profile.Id, "TESTBALL_FULL", "TB_REFEREE", "Class 1", "UG-REF 0147", (DateOnly?)new DateOnly(2027, 6, 30)),
            (profile.Id, "TESTHOOP_FULL", "TH_REFEREE", null, null, null),
        ]);
        var row = person.ProfileStatusHistory.ShouldHaveSingleItem();
        (row.ProfileKind, row.ProfileId, row.FromStatus, row.ToStatus, row.Reason)
            .ShouldBe((ProfileKind.Official, profile.Id, (ProfileStatus?)null, ProfileStatus.Active, (string?)null));
    }

    [Fact]
    public void One_open_official_profile_and_a_retired_one_counts()
    {
        var person = NewPerson();
        var first = person.AddOfficialProfile([_referee], SystemActor.Instance, _now).Value;

        person.AddOfficialProfile([_assistant], SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.OfficialProfileExists);
        person.ChangeOfficialProfileStatus(first.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddOfficialProfile([_assistant], SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.OfficialProfileExists);

        person.ChangeOfficialProfileStatus(first.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddOfficialProfile([_assistant], SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.OfficialProfiles.Count.ShouldBe(2);
    }

    [Fact]
    public void An_update_keeps_the_capabilities_held_removes_the_ones_left_out_and_adds_the_new_ones()
    {
        var person = NewPerson();
        var profile = person.AddOfficialProfile([_referee, _assistant], SystemActor.Instance, _now).Value;
        var referee = profile.Capabilities.Single(c => c.Role == "TB_REFEREE");
        var renewed = _referee with { Grade = null, LicenceNumber = "UG-REF-0200", LicenceValidUntil = new DateOnly(2028, 6, 30) };

        person.UpdateOfficialProfile(profile.Id, [renewed, _hoopReferee]).IsSuccess.ShouldBeTrue();

        profile.Capabilities.Select(c => (c.Role, c.Grade, c.LicenceNumber, c.LicenceValidUntil)).ShouldBe(
        [
            ("TB_REFEREE", null, "UG-REF-0200", (DateOnly?)new DateOnly(2028, 6, 30)),
            ("TH_REFEREE", (string?)null, (string?)null, (DateOnly?)null),
        ]);
        profile.Capabilities.ShouldContain(referee);
        profile.Holds("TESTBALL_FULL", "TB_ASSISTANT").ShouldBeFalse();
        person.UpdateOfficialProfile(Guid.CreateVersion7(), [_referee]).Error.ShouldBe(RegistryErrors.ProfileNotFound);

        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.UpdateOfficialProfile(profile.Id, [_referee]).Error.ShouldBe(RegistryErrors.ProfileArchived);
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_gets_no_profile_and_none_back_to_active(PersonTransition transition, string code)
    {
        var person = NewPerson();
        var profile = person.AddOfficialProfile([_referee, _hoopReferee], SystemActor.Instance, _now).Value;
        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Inactive, "On leave", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.AddOfficialProfile([_assistant], SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);
        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);

        // A new role is refused (as staff, review #102 R2); keeping, removing and re-licensing held ones stay allowed.
        person.UpdateOfficialProfile(profile.Id, [_referee, _assistant]).Error.ShouldNotBeNull().Code.ShouldBe(code);
        person.UpdateOfficialProfile(profile.Id, [_referee with { LicenceNumber = "UG-REF-0200" }]).IsSuccess.ShouldBeTrue();
        profile.Capabilities.ShouldHaveSingleItem().LicenceNumber.ShouldBe("UG-REF-0200");
        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_move_writes_an_official_history_row_and_other_kinds_do_not_find_the_profile()
    {
        var person = NewPerson();
        var profile = person.AddOfficialProfile([_referee], SystemActor.Instance, _now).Value;

        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Inactive, " On leave ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.ChangeOfficialProfileStatus(profile.Id, ProfileStatus.Inactive, "Again", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileStatusInvalid);

        var row = person.ProfileStatusHistory[^1];
        (row.ProfileKind, row.ProfileId, row.FromStatus, row.ToStatus, row.Reason)
            .ShouldBe((ProfileKind.Official, profile.Id, (ProfileStatus?)ProfileStatus.Active, ProfileStatus.Inactive, "On leave"));
        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileNotFound);
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileNotFound);
    }

    [Theory]
    [InlineData("2027-06-29", false)]
    [InlineData("2027-06-30", false)]
    [InlineData("2027-07-01", true)]
    public void A_licence_is_valid_through_its_last_day(string today, bool expired)
    {
        var profile = NewPerson().AddOfficialProfile([_referee, _assistant], SystemActor.Instance, _now).Value;

        profile.Capabilities.Single(c => c.Role == "TB_REFEREE").LicenceExpiredOn(DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(expired);
        profile.Capabilities.Single(c => c.Role == "TB_ASSISTANT").LicenceExpiredOn(DateOnly.MaxValue).ShouldBeFalse(); // no date
    }

    [Fact]
    public void Capabilities_are_one_to_ten_and_distinct_with_bounded_texts()
    {
        var person = NewPerson();
        var eleven = Enumerable.Range(0, 11).Select(i => new OfficialCapabilityDetails("TESTBALL_FULL", $"TB_ROLE_{i}", null, null, null)).ToArray();

        Should.Throw<ArgumentException>(() => person.AddOfficialProfile([], SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddOfficialProfile(eleven, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddOfficialProfile([_referee, _referee with { Grade = null }], SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddOfficialProfile(
            [_referee with { Grade = new string('G', OfficialCapability.GradeMaxLength + 1) }], SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddOfficialProfile(
            [_referee with { LicenceNumber = new string('L', OfficialCapability.LicenceNumberMaxLength + 1) }], SystemActor.Instance, _now));
        person.OfficialProfiles.ShouldBeEmpty();
        person.ProfileStatusHistory.ShouldBeEmpty();
        person.AddOfficialProfile([.. eleven.Take(10)], SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    private static Person NewPerson() =>
        Person.Create(new PersonBiodata("Denis", "Opoka"), PersonSource.Manual, SystemActor.Instance, _now);
}
