using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

// S06 D9, D10: aliases and external identifiers on a person, and the statuses that take new ones.
public sealed class PersonAliasesAndIdentifiersTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _today = new(2026, 11, 17);

    [Theory]
    [InlineData(PersonStatus.Active, null)]
    [InlineData(PersonStatus.Inactive, null)]
    [InlineData(PersonStatus.Archived, "PERSON_ARCHIVED")]
    [InlineData(PersonStatus.Deceased, "PERSON_DECEASED")]
    public void Only_an_active_or_inactive_person_takes_new_aliases(PersonStatus status, string? refusal)
    {
        var person = PersonIn(status);

        var accepts = person.EnsureAcceptsNewChildren();
        var added = person.AddAlias("Kasa", AliasKind.Nickname);

        accepts.IsSuccess.ShouldBe(refusal is null);
        added.IsSuccess.ShouldBe(refusal is null);
        if (refusal is not null)
        {
            accepts.Error.ShouldNotBeNull().Code.ShouldBe(refusal);
            added.Error.ShouldNotBeNull().Code.ShouldBe(refusal);
            person.Aliases.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("Mukasa Ivan")]
    [InlineData("IVAN  mukasa")]
    [InlineData("Kása")]
    public void An_alias_with_a_name_key_the_person_already_has_is_refused(string name)
    {
        var person = PersonIn(PersonStatus.Active);
        person.AddAlias("Kasa", AliasKind.Nickname).IsSuccess.ShouldBeTrue();

        person.AddAlias(name, AliasKind.Spelling).Error.ShouldBe(RegistryErrors.AliasExists);
        person.Aliases.Count.ShouldBe(1);
    }

    [Fact]
    public void A_removed_alias_keeps_who_when_and_why_and_its_name_may_come_back()
    {
        var person = PersonIn(PersonStatus.Active);
        var alias = person.AddAlias("Kasa", AliasKind.Nickname).Value;
        var actor = new UserActor(Guid.CreateVersion7());

        person.RemoveAlias(alias.Id, " Misspelt ", actor, _now).IsSuccess.ShouldBeTrue();

        (alias.IsRemoved, alias.RemovedAt, alias.RemovedBy, alias.RemoveReason).ShouldBe((true, (DateTimeOffset?)_now, (Guid?)actor.ActorId, "Misspelt"));
        person.RemoveAlias(alias.Id, "Misspelt", actor, _now).Error.ShouldBe(RegistryErrors.AliasRemoved);
        person.RemoveAlias(Guid.CreateVersion7(), "Misspelt", actor, _now).Error.ShouldBe(RegistryErrors.AliasNotFound);
        person.AddAlias("Kasa", AliasKind.Nickname).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(PersonStatus.Archived)]
    [InlineData(PersonStatus.Deceased)]
    public void An_archived_or_deceased_persons_alias_can_still_be_removed(PersonStatus status)
    {
        var person = PersonIn(PersonStatus.Active);
        var alias = person.AddAlias("Kasa", AliasKind.Nickname).Value;
        Move(person, status);

        person.RemoveAlias(alias.Id, "Misspelt", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_identifier_ends_once_between_its_start_and_today()
    {
        var identifier = ExternalIdentifier.ForPerson(Guid.CreateVersion7(), "KMSA_REG_NO", "KM-2026-0147", _today.AddDays(-30));

        identifier.End(_today.AddDays(1), _today).Error.ShouldBe(RegistryErrors.ExternalIdValidityInvalid);
        identifier.End(_today.AddDays(-31), _today).Error.ShouldBe(RegistryErrors.ExternalIdValidityInvalid);
        identifier.IsEnded.ShouldBeFalse();

        identifier.End(_today.AddDays(-30), _today).IsSuccess.ShouldBeTrue();

        identifier.ValidTo.ShouldBe(_today.AddDays(-30));
        identifier.End(_today, _today).Error.ShouldBe(RegistryErrors.ExternalIdEnded);
    }

    [Fact]
    public void An_identifier_without_a_start_ends_on_any_day_up_to_today() =>
        ExternalIdentifier.ForPerson(Guid.CreateVersion7(), "KMSA_REG_NO", "KM-2026-0147")
            .End(new DateOnly(2001, 1, 1), _today).IsSuccess.ShouldBeTrue();

    private static Person PersonIn(PersonStatus status)
    {
        var person = Person.Create(new PersonBiodata("Ivan", "Mukasa"), PersonSource.Manual, SystemActor.Instance, _now);
        Move(person, status);
        return person;
    }

    private static void Move(Person person, PersonStatus status)
    {
        PersonTransition? transition = status switch
        {
            PersonStatus.Inactive => PersonTransition.Deactivate,
            PersonStatus.Archived => PersonTransition.Archive,
            PersonStatus.Deceased => PersonTransition.MarkDeceased,
            _ => null,
        };
        if (transition is { } t)
        {
            person.ChangeStatus(t, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        }
    }
}
