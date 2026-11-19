using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;

namespace SportsPlatform.UnitTests.Registry;

// S06 D9, D14: consent for anyone not known to be an adult; withdrawing closes the record and keeps what remains in a
// successor; evidence on an active consent only.
public sealed class GuardianConsentTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private static readonly ConsentScope[] _all = [ConsentScope.DataProcessing, ConsentScope.PublicProfile, ConsentScope.Photo];

    [Fact]
    public void A_recorded_consent_is_active_with_its_scopes_and_guardian()
    {
        var person = Brenda();

        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;

        consent.Status.ShouldBe(ConsentStatus.Active);
        consent.Scopes.ShouldBe(_all);
        consent.GuardianName.ShouldBe("Grace Akello");
        consent.Relationship.ShouldBe(GuardianRelationship.Mother);
        consent.Contact.ShouldBe("+256 772 410 233");
        consent.GivenOn.ShouldBe(new DateOnly(2026, 11, 2));
        consent.RecordedAt.ShouldBe(_now);
        person.ActiveConsentScopes.ShouldBe(_all, ignoreOrder: true);
    }

    [Fact]
    public void Several_guardians_consent_and_the_active_scopes_are_theirs_together()
    {
        var person = Brenda();
        person.RecordConsent(Mother([ConsentScope.DataProcessing]), AgeStatus.Minor, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        var father = new GuardianConsentDetails(
            "Samuel Okello", GuardianRelationship.Father, "samuel.okello@example.ug", [ConsentScope.Photo], new DateOnly(2026, 11, 3));

        person.RecordConsent(father, AgeStatus.Minor, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.ActiveConsentScopes.ShouldBe([ConsentScope.DataProcessing, ConsentScope.Photo], ignoreOrder: true);
    }

    [Theory]
    [InlineData(AgeStatus.Minor, null)]
    [InlineData(AgeStatus.Unknown, null)]
    [InlineData(AgeStatus.Adult, "PERSON_CONSENT_NOT_NEEDED")]
    public void Consent_is_recorded_for_anyone_not_known_to_be_an_adult(AgeStatus ageStatus, string? refusal)
    {
        var person = Brenda();

        var recorded = person.RecordConsent(Mother(_all), ageStatus, SystemActor.Instance, _now);

        recorded.Error?.Code.ShouldBe(refusal);
        person.Consents.Count.ShouldBe(refusal is null ? 1 : 0);
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_takes_no_new_consent_or_evidence_but_one_can_be_withdrawn(
        PersonTransition transition, string refusal)
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Error!.Code.ShouldBe(refusal);
        person.AttachConsentEvidence(consent.Id, Guid.CreateVersion7()).Error!.Code.ShouldBe(refusal);

        person.WithdrawConsent(consent.Id, _all, "Guardian asked", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Withdrawing_a_scope_closes_the_record_and_records_a_successor_with_the_rest()
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;
        var evidence = Guid.CreateVersion7();
        person.AttachConsentEvidence(consent.Id, evidence).IsSuccess.ShouldBeTrue();
        var later = _now.AddDays(3);

        var successor = person.WithdrawConsent(consent.Id, [ConsentScope.Photo], "  Guardian asked  ", SystemActor.Instance, later).Value;

        consent.Status.ShouldBe(ConsentStatus.Withdrawn);
        consent.WithdrawalReason.ShouldBe("Guardian asked");
        consent.WithdrawnAt.ShouldBe(later);
        consent.WithdrawnBy.ShouldBe(SystemActor.Instance.ActorId);
        consent.Scopes.ShouldBe(_all);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(ConsentStatus.Active);
        successor.Scopes.ShouldBe([ConsentScope.DataProcessing, ConsentScope.PublicProfile]);
        successor.ReplacesConsentId.ShouldBe(consent.Id);
        (successor.GuardianName, successor.Relationship, successor.Contact, successor.GivenOn, successor.EvidenceAssetId)
            .ShouldBe((consent.GuardianName, consent.Relationship, consent.Contact, consent.GivenOn, evidence));
        successor.RecordedAt.ShouldBe(later);
        person.Consents.Count.ShouldBe(2);
        person.ActiveConsentScopes.ShouldBe([ConsentScope.DataProcessing, ConsentScope.PublicProfile], ignoreOrder: true);
    }

    [Fact]
    public void Withdrawing_every_scope_records_no_successor()
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;

        person.WithdrawConsent(consent.Id, _all, "Guardian asked", SystemActor.Instance, _now).Value.ShouldBeNull();

        person.Consents.ShouldHaveSingleItem().Status.ShouldBe(ConsentStatus.Withdrawn);
        person.ActiveConsentScopes.ShouldBeEmpty();
    }

    [Fact]
    public void A_withdrawn_or_unknown_consent_is_refused()
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;
        person.WithdrawConsent(consent.Id, [ConsentScope.Photo], "Guardian asked", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.WithdrawConsent(consent.Id, [ConsentScope.DataProcessing], "Guardian asked", SystemActor.Instance, _now)
            .Error.ShouldBe(RegistryErrors.ConsentWithdrawn);
        person.AttachConsentEvidence(consent.Id, Guid.CreateVersion7()).Error.ShouldBe(RegistryErrors.ConsentWithdrawn);
        person.WithdrawConsent(Guid.CreateVersion7(), [ConsentScope.Photo], "Guardian asked", SystemActor.Instance, _now)
            .Error.ShouldBe(RegistryErrors.ConsentNotFound);
        person.AttachConsentEvidence(Guid.CreateVersion7(), Guid.CreateVersion7()).Error.ShouldBe(RegistryErrors.ConsentNotFound);
    }

    [Fact]
    public void Withdrawing_a_scope_the_consent_does_not_give_is_a_bug()
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother([ConsentScope.DataProcessing]), AgeStatus.Minor, SystemActor.Instance, _now).Value;

        Should.Throw<ArgumentException>(
            () => person.WithdrawConsent(consent.Id, [ConsentScope.Photo], "Guardian asked", SystemActor.Instance, _now));
        consent.Status.ShouldBe(ConsentStatus.Active);
    }

    [Fact]
    public void Attaching_evidence_returns_the_one_it_replaces()
    {
        var person = Brenda();
        var consent = person.RecordConsent(Mother(_all), AgeStatus.Minor, SystemActor.Instance, _now).Value;
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        person.AttachConsentEvidence(consent.Id, first).Value.ShouldBeNull();
        person.AttachConsentEvidence(consent.Id, second).Value.ShouldBe(first);

        consent.EvidenceAssetId.ShouldBe(second);
    }

    [Fact]
    public void A_consent_needs_distinct_scopes_and_a_guardian()
    {
        var person = Brenda();

        Should.Throw<ArgumentException>(() => person.RecordConsent(Mother([]), AgeStatus.Minor, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(
            () => person.RecordConsent(Mother([ConsentScope.Photo, ConsentScope.Photo]), AgeStatus.Minor, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(
            () => person.RecordConsent(Mother(_all) with { GuardianName = " " }, AgeStatus.Minor, SystemActor.Instance, _now));
        person.Consents.ShouldBeEmpty();
    }

    private static Person Brenda() =>
        Person.Create(
            new PersonBiodata("Brenda", "Akello", DateOfBirth: new DateOnly(2011, 3, 2)), PersonSource.Manual, SystemActor.Instance, _now);

    private static GuardianConsentDetails Mother(IReadOnlyCollection<ConsentScope> scopes) =>
        new("Grace Akello", GuardianRelationship.Mother, "+256 772 410 233", scopes, new DateOnly(2026, 11, 2));
}
