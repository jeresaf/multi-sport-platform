using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.UnitTests.Registry;

// S06 D12 (owner, S06-T4 plan): athlete profiles on a person, and every profile status against every target. The codes
// are test codes: the domain never reads them (AR-11).
public sealed class AthleteProfileTests
{
    private const string _variant = "TESTBALL_FULL";

    private static readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly AthleteProfileDetails _midfielder = new("TB_MID", ["TB_WING"], DominantSide.Right, "J. Ssekandi");

    // The allowed moves; every other pair is refused.
    private static readonly HashSet<(ProfileStatus, ProfileStatus)> _allowed =
    [
        (ProfileStatus.Active, ProfileStatus.Inactive),
        (ProfileStatus.Inactive, ProfileStatus.Active),
        (ProfileStatus.Active, ProfileStatus.Retired),
        (ProfileStatus.Inactive, ProfileStatus.Retired),
        (ProfileStatus.Retired, ProfileStatus.Active),
        (ProfileStatus.Active, ProfileStatus.Archived),
        (ProfileStatus.Inactive, ProfileStatus.Archived),
        (ProfileStatus.Retired, ProfileStatus.Archived),
    ];

    public static TheoryData<ProfileStatus, ProfileStatus> EveryPair()
    {
        var data = new TheoryData<ProfileStatus, ProfileStatus>();
        foreach (var from in Enum.GetValues<ProfileStatus>())
        {
            foreach (var to in Enum.GetValues<ProfileStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void A_new_profile_is_active_with_its_first_history_row()
    {
        var person = NewPerson();

        var profile = person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Value;

        (profile.PersonId, profile.Variant, profile.Status).ShouldBe((person.Id, _variant, ProfileStatus.Active));
        (profile.PrimaryPosition, profile.DominantSide, profile.PublicName).ShouldBe(("TB_MID", DominantSide.Right, "J. Ssekandi"));
        profile.OtherPositions.ShouldBe(["TB_WING"]);
        var row = person.ProfileStatusHistory.ShouldHaveSingleItem();
        (row.ProfileKind, row.ProfileId, row.FromStatus, row.ToStatus, row.Reason, row.OccurredAt)
            .ShouldBe((ProfileKind.Athlete, profile.Id, (ProfileStatus?)null, ProfileStatus.Active, (string?)null, _now));
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Each_move_is_allowed_only_from_its_states_and_writes_a_history_row(ProfileStatus from, ProfileStatus to)
    {
        var (person, profile) = ProfileIn(from);
        var rows = person.ProfileStatusHistory.Count;

        var result = person.ChangeAthleteProfileStatus(profile.Id, to, " Moved to the under-20s ", SystemActor.Instance, _now);

        if (_allowed.Contains((from, to)))
        {
            result.IsSuccess.ShouldBeTrue();
            profile.Status.ShouldBe(to);
            var row = person.ProfileStatusHistory[^1];
            person.ProfileStatusHistory.Count.ShouldBe(rows + 1);
            (row.ProfileId, row.FromStatus, row.ToStatus, row.Reason).ShouldBe((profile.Id, (ProfileStatus?)from, to, "Moved to the under-20s"));
        }
        else
        {
            result.Error.ShouldBe(RegistryErrors.ProfileStatusInvalid);
            profile.Status.ShouldBe(from);
            person.ProfileStatusHistory.Count.ShouldBe(rows);
        }
    }

    [Fact]
    public void One_open_profile_per_variant_and_a_retired_one_counts()
    {
        var person = NewPerson();
        var first = person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Value;

        person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.AthleteProfileExists);
        person.AddAthleteProfile("TESTBALL_SHORT", _midfielder, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.ChangeAthleteProfileStatus(first.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Error.ShouldBe(RegistryErrors.AthleteProfileExists);

        // Archived is closed: a new profile for the variant may start.
        person.ChangeAthleteProfileStatus(first.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_gets_no_profile_and_none_back_to_active(PersonTransition transition, string code)
    {
        var person = NewPerson();
        var profile = person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Value;
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Inactive, "Injured", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.AddAthleteProfile("TESTBALL_SHORT", _midfielder, SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Active, "Back", SystemActor.Instance, _now).Error.ShouldNotBeNull().Code.ShouldBe(code);

        // Corrections and closing moves stay allowed.
        person.UpdateAthleteProfile(profile.Id, _midfielder with { PublicName = null }).IsSuccess.ShouldBeTrue();
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        profile.Status.ShouldBe(ProfileStatus.Retired);
    }

    [Fact]
    public void An_update_replaces_the_details_but_not_on_an_archived_profile()
    {
        var person = NewPerson();
        var profile = person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Value;

        person.UpdateAthleteProfile(profile.Id, new AthleteProfileDetails(null, [], null, "  Johnny   S. ")).IsSuccess.ShouldBeTrue();

        (profile.PrimaryPosition, profile.DominantSide, profile.PublicName).ShouldBe(((string?)null, (DominantSide?)null, "Johnny S."));
        profile.OtherPositions.ShouldBeEmpty();
        person.UpdateAthleteProfile(Guid.CreateVersion7(), _midfielder).Error.ShouldBe(RegistryErrors.ProfileNotFound);
        person.ChangeAthleteProfileStatus(profile.Id, ProfileStatus.Archived, "Recorded by mistake", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        person.UpdateAthleteProfile(profile.Id, _midfielder).Error.ShouldBe(RegistryErrors.ProfileArchived);
    }

    [Fact]
    public void Positions_are_distinct_at_most_five_and_never_the_primary_one()
    {
        var person = NewPerson();

        Should.Throw<ArgumentException>(() => person.AddAthleteProfile(_variant, _midfielder with { OtherPositions = ["TB_MID"] }, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddAthleteProfile(_variant, _midfielder with { OtherPositions = ["TB_WING", "TB_WING"] }, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => person.AddAthleteProfile(
            _variant, _midfielder with { OtherPositions = ["A", "B", "C", "D", "E", "F"] }, SystemActor.Instance, _now));
        person.ProfileStatusHistory.ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_profile_is_not_found()
    {
        NewPerson().ChangeAthleteProfileStatus(Guid.CreateVersion7(), ProfileStatus.Inactive, "Injured", SystemActor.Instance, _now)
            .Error.ShouldBe(RegistryErrors.ProfileNotFound);
    }

    private static Person NewPerson() =>
        Person.Create(new PersonBiodata("John", "Ssekandi"), PersonSource.Manual, SystemActor.Instance, _now);

    private static (Person Person, AthleteProfile Profile) ProfileIn(ProfileStatus status)
    {
        var person = NewPerson();
        var profile = person.AddAthleteProfile(_variant, _midfielder, SystemActor.Instance, _now).Value;
        ProfileStatus[] path = status switch
        {
            ProfileStatus.Active => [],
            ProfileStatus.Inactive => [ProfileStatus.Inactive],
            ProfileStatus.Retired => [ProfileStatus.Retired],
            ProfileStatus.Archived => [ProfileStatus.Archived],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        foreach (var to in path)
        {
            person.ChangeAthleteProfileStatus(profile.Id, to, "Set up for the test", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        }

        return (person, profile);
    }
}
