using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.UnitTests.Registry;

// S06 D12 (owner, S06-T4b plan): a staff profile on a person, its capabilities replaced as a whole, its moves. The
// codes are test codes: the domain never reads them (AR-11). The state machine itself is AthleteProfileTests' (shared).
public sealed class StaffProfileTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly StaffCapabilityDetails _coach = new("TESTBALL_FULL", "TB_COACH", "  Level 2   coaching ");
    private static readonly StaffCapabilityDetails _physio = new("TESTBALL_FULL", "TB_PHYSIO", null);
    private static readonly StaffCapabilityDetails _hoopCoach = new("TESTHOOP_FULL", "TH_COACH", null);

    [Fact]
    public void A_new_profile_is_active_with_its_capabilities_and_its_first_history_row()
    {
        var person = NewPerson();

        var profile = person.AddStaffProfile([_coach, _hoopCoach], SystemActor.Instance, _now).Value;

        (profile.PersonId, profile.Status).ShouldBe((person.Id, ProfileStatus.Active));
        profile.Capabilities.Select(c => (c.StaffProfileId, c.Variant, c.Role, c.Qualification)).ShouldBe(
        [
            (profile.Id, "TESTBALL_FULL", "TB_COACH", "Level 2 coaching"),
            (profile.Id, "TESTHOOP_FULL", "TH_COACH", null),
        ]);
        var row = person.ProfileStatusHistory.ShouldHaveSingleItem();
        (row.ProfileKind, row.ProfileId, row.FromStatus, row.ToStatus, row.Reason)
            .ShouldBe((ProfileKind.Staff, profile.Id, (ProfileStatus?)null, ProfileStatus.Active, (string?)null));
    }

    [Fact]
    public void One_open_staff_profile_and_a_retired_one_counts()
    {
        var person = NewPerson();
        var first = person.AddStaffProfile([_coach], SystemActor.Instance, _now).Value;

        person.AddStaffProfile([_physio], SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.StaffProfileExists);
        person.ChangeStaffProfileStatus(first.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddStaffProfile([_physio], SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.StaffProfileExists);

        person.ChangeStaffProfileStatus(first.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddStaffProfile([_physio], SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.StaffProfiles.Count.ShouldBe(2);
    }

    [Fact]
    public void An_update_keeps_the_capabilities_held_removes_the_ones_left_out_and_adds_the_new_ones()
    {
        var person = NewPerson();
        var profile = person.AddStaffProfile([_coach, _physio], SystemActor.Instance, _now).Value;
        var coach = profile.Capabilities.Single(c => c.Role == "TB_COACH");

        person.UpdateStaffProfile(profile.Id, [_coach with { Qualification = "Level 3" }, _hoopCoach]).IsSuccess.ShouldBeTrue();

        profile.Capabilities.Select(c => (c.Role, c.Qualification)).ShouldBe([("TB_COACH", "Level 3"), ("TH_COACH", (string?)null)]);
        profile.Capabilities.ShouldContain(coach);
        profile.Holds("TESTBALL_FULL", "TB_COACH").ShouldBeTrue();
        profile.Holds("TESTBALL_FULL", "TB_PHYSIO").ShouldBeFalse();
        person.UpdateStaffProfile(Guid.CreateVersion7(), [_coach]).Error.ShouldBe(RegistryErrors.ProfileNotFound);

        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.UpdateStaffProfile(profile.Id, [_coach]).Error.ShouldBe(RegistryErrors.ProfileArchived);
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_gets_no_profile_and_none_back_to_active(PersonTransition transition, string code)
    {
        var person = NewPerson();
        var profile = person.AddStaffProfile([_coach, _hoopCoach], SystemActor.Instance, _now).Value;
        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Inactive, "On leave", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.AddStaffProfile([_physio], SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);
        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);

        // A new role is refused (review #102 R2); keeping, removing and re-qualifying held ones stay allowed, as do closing moves.
        person.UpdateStaffProfile(profile.Id, [_coach, _physio]).Error.ShouldNotBeNull().Code.ShouldBe(code);
        person.UpdateStaffProfile(profile.Id, [_coach with { Qualification = "Level 3" }]).IsSuccess.ShouldBeTrue(); // removes the hoop coach
        profile.Capabilities.ShouldHaveSingleItem().Qualification.ShouldBe("Level 3");
        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_move_writes_a_staff_history_row_and_an_invalid_one_is_refused()
    {
        var person = NewPerson();
        var profile = person.AddStaffProfile([_coach], SystemActor.Instance, _now).Value;

        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Inactive, " On leave ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.ChangeStaffProfileStatus(profile.Id, ProfileStatus.Inactive, "Again", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileStatusInvalid);
        person.ChangeStaffProfileStatus(Guid.CreateVersion7(), ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileNotFound);

        var row = person.ProfileStatusHistory[^1];
        (row.ProfileKind, row.ProfileId, row.FromStatus, row.ToStatus, row.Reason)
            .ShouldBe((ProfileKind.Staff, profile.Id, (ProfileStatus?)ProfileStatus.Active, ProfileStatus.Inactive, "On leave"));

        // An athlete move doesn't find a staff profile, nor the other way round.
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error
            .ShouldBe(RegistryErrors.ProfileNotFound);
    }

    [Fact]
    public void Capabilities_are_one_to_ten_and_distinct()
    {
        var person = NewPerson();
        var eleven = Enumerable.Range(0, 11).Select(i => new StaffCapabilityDetails("TESTBALL_FULL", $"TB_ROLE_{i}", null)).ToArray();

        Should.Throw<ArgumentException>(() => person.AddStaffProfile([], SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddStaffProfile(eleven, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddStaffProfile([_coach, _coach with { Qualification = null }], SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddStaffProfile(
            [_coach with { Qualification = new string('Q', StaffCapability.QualificationMaxLength + 1) }], SystemActor.Instance, _now));
        person.StaffProfiles.ShouldBeEmpty();
        person.ProfileStatusHistory.ShouldBeEmpty();
        person.AddStaffProfile([.. eleven.Take(10)], SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    private static Person NewPerson() =>
        Person.Create(new PersonBiodata("Denis", "Opoka"), PersonSource.Manual, SystemActor.Instance, _now);
}
