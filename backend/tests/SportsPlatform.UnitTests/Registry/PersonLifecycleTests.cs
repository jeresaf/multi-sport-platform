using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

// S06 D8, D9: changing a person's biodata, and every status against every transition.
public sealed class PersonLifecycleTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly PersonBiodata _john = new(
        "John", "Ssekandi", Gender: Gender.Male, DateOfBirth: new DateOnly(2001, 4, 12), Nationality: "UG", HeightCm: 178);

    // The allowed transitions (D9); every other pair is refused.
    private static readonly Dictionary<(PersonStatus, PersonTransition), PersonStatus> _allowed = new()
    {
        [(PersonStatus.Active, PersonTransition.Deactivate)] = PersonStatus.Inactive,
        [(PersonStatus.Inactive, PersonTransition.Reactivate)] = PersonStatus.Active,
        [(PersonStatus.Active, PersonTransition.Archive)] = PersonStatus.Archived,
        [(PersonStatus.Inactive, PersonTransition.Archive)] = PersonStatus.Archived,
        [(PersonStatus.Archived, PersonTransition.Unarchive)] = PersonStatus.Active,
        [(PersonStatus.Active, PersonTransition.MarkDeceased)] = PersonStatus.Deceased,
        [(PersonStatus.Inactive, PersonTransition.MarkDeceased)] = PersonStatus.Deceased,
    };

    public static TheoryData<PersonStatus, PersonTransition> EveryPair()
    {
        var data = new TheoryData<PersonStatus, PersonTransition>();
        foreach (var status in Enum.GetValues<PersonStatus>())
        {
            foreach (var transition in Enum.GetValues<PersonTransition>())
            {
                data.Add(status, transition);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Each_transition_is_allowed_only_from_its_states_and_writes_a_history_row(PersonStatus from, PersonTransition transition)
    {
        var person = PersonIn(from);
        var rows = person.StatusHistory.Count;
        var reason = PersonTransitions.NeedsReason(transition) ? " Left the association " : null;

        var result = person.ChangeStatus(transition, reason, SystemActor.Instance, _now);

        if (_allowed.TryGetValue((from, transition), out var to))
        {
            result.IsSuccess.ShouldBeTrue();
            person.Status.ShouldBe(to);
            person.StatusHistory.Count.ShouldBe(rows + 1);
            var row = person.StatusHistory[^1];
            (row.FromStatus, row.ToStatus, row.Reason, row.OccurredAt).ShouldBe((from, to, reason?.Trim(), _now));
        }
        else
        {
            result.Error.ShouldBe(RegistryErrors.StatusInvalid);
            person.Status.ShouldBe(from);
            person.StatusHistory.Count.ShouldBe(rows);
        }
    }

    [Theory]
    [InlineData(PersonTransition.Archive)]
    [InlineData(PersonTransition.Unarchive)]
    [InlineData(PersonTransition.MarkDeceased)]
    public void Archiving_unarchiving_and_marking_deceased_need_a_reason(PersonTransition transition)
    {
        PersonTransitions.NeedsReason(transition).ShouldBeTrue();
        Should.Throw<ArgumentException>(() => PersonIn(PersonStatus.Archived).ChangeStatus(transition, " ", SystemActor.Instance, _now));
    }

    [Fact]
    public void Deactivating_and_reactivating_need_no_reason()
    {
        PersonTransitions.NeedsReason(PersonTransition.Deactivate).ShouldBeFalse();
        PersonTransitions.NeedsReason(PersonTransition.Reactivate).ShouldBeFalse();
    }

    [Fact]
    public void An_update_replaces_the_biodata_and_its_keys_and_keeps_the_status()
    {
        var person = PersonIn(PersonStatus.Archived);

        person.Update(new PersonBiodata("Jon", "Ssekandi-Mukasa", MiddleName: "Paul", Nationality: "KE"));

        (person.FirstName, person.MiddleName, person.LastName, person.DisplayName).ShouldBe(("Jon", "Paul", "Ssekandi-Mukasa", "Jon Ssekandi-Mukasa"));
        (person.FirstNameKey, person.LastNameKey, person.NameKey).ShouldBe(("jon", "ssekandimukasa", "jon paul ssekandimukasa"));
        (person.Gender, person.DateOfBirth, person.Nationality, person.HeightCm).ShouldBe((null, null, "KE", null));
        person.Status.ShouldBe(PersonStatus.Archived);
    }

    public static TheoryData<PersonBiodata, bool> Changes => new()
    {
        // What the check doesn't compare.
        { _john with { DisplayName = "J. Ssekandi", Gender = null, HeightCm = 180, WeightKg = 71 }, false },
        // The same keys: case, accents and punctuation.
        { _john with { FirstName = "JOHN", LastName = "Ssékandi." }, false },
        { _john with { FirstName = "Jon" }, true },
        // Swapping first and last name keeps the name key but not the last name.
        { _john with { FirstName = "Ssekandi", LastName = "John" }, true },
        { _john with { MiddleName = "Paul" }, true },
        { _john with { OtherNames = "Mmamba" }, true },
        { _john with { DateOfBirth = new DateOnly(2001, 4, 13) }, true },
        { _john with { DateOfBirth = null }, true },
        { _john with { Nationality = "KE" }, true },
        { _john with { SecondaryNationality = "KE" }, true },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void Only_a_change_to_what_the_check_compares_changes_the_identity(PersonBiodata biodata, bool changes) =>
        Person.Create(_john, PersonSource.Manual, SystemActor.Instance, _now).ChangesIdentity(biodata).ShouldBe(changes);

    // A person in a status, reached through the transitions.
    private static Person PersonIn(PersonStatus status)
    {
        var person = Person.Create(_john, PersonSource.Manual, SystemActor.Instance, _now);
        var path = status switch
        {
            PersonStatus.Active => [],
            PersonStatus.Inactive => [PersonTransition.Deactivate],
            PersonStatus.Archived => [PersonTransition.Archive],
            PersonStatus.Deceased => new[] { PersonTransition.MarkDeceased },
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        foreach (var transition in path)
        {
            person.ChangeStatus(transition, "Set up for the test", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        }

        person.Status.ShouldBe(status);
        return person;
    }
}
