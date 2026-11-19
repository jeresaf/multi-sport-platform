using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.UnitTests.Registry;

// S06 D15, D18, ADR-0015: the public profile needs PUBLIC_PROFILE consent unless the person is an adult; what the public
// read shows follows the status, the flag, the age status and the active scopes, checked every time.
public sealed class PublicProfileTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private static readonly ConsentScope[] _none = [];

    private static readonly ConsentScope[] _publicProfile = [ConsentScope.PublicProfile];

    private static readonly ConsentScope[] _publicProfileAndPhoto = [ConsentScope.PublicProfile, ConsentScope.Photo];

    [Theory]
    [InlineData(AgeStatus.Minor)]
    [InlineData(AgeStatus.Unknown)]
    public void A_person_not_known_to_be_an_adult_needs_public_profile_consent(AgeStatus ageStatus)
    {
        var person = Brenda();

        person.EnablePublicProfile(ageStatus, [ConsentScope.DataProcessing, ConsentScope.Photo]).Error!.Code
            .ShouldBe("PERSON_CONSENT_REQUIRED");
        person.PublicProfileEnabled.ShouldBeFalse();

        person.EnablePublicProfile(ageStatus, _publicProfile).IsSuccess.ShouldBeTrue();
        person.PublicProfileEnabled.ShouldBeTrue();
    }

    // Review #106 N1: the scope may come from any active consent, a second guardian's included.
    [Fact]
    public void The_scope_from_a_second_guardians_consent_is_enough()
    {
        var person = Brenda();
        person.RecordConsent(Guardian("Grace Akello", GuardianRelationship.Mother, ConsentScope.DataProcessing), AgeStatus.Minor, SystemActor.Instance, _now);
        person.RecordConsent(Guardian("Samuel Okello", GuardianRelationship.Father, ConsentScope.PublicProfile), AgeStatus.Minor, SystemActor.Instance, _now);

        person.EnablePublicProfile(AgeStatus.Minor, GuardianConsent.ActiveScopes(person.Consents)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_adult_needs_no_consent()
    {
        var person = Brenda();

        person.EnablePublicProfile(AgeStatus.Adult, _none).IsSuccess.ShouldBeTrue();
        person.PublicProfileEnabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_is_not_turned_on_but_is_turned_off(PersonTransition transition, string refusal)
    {
        var person = Brenda();
        person.EnablePublicProfile(AgeStatus.Adult, _none).IsSuccess.ShouldBeTrue();
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        person.DisablePublicProfile();
        person.PublicProfileEnabled.ShouldBeFalse();
        person.EnablePublicProfile(AgeStatus.Adult, _none).Error!.Code.ShouldBe(refusal);
        person.PublicProfileEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Turning_on_twice_or_off_twice_changes_nothing()
    {
        var person = Brenda();

        person.EnablePublicProfile(AgeStatus.Adult, _none).IsSuccess.ShouldBeTrue();
        person.EnablePublicProfile(AgeStatus.Adult, _none).IsSuccess.ShouldBeTrue();
        person.PublicProfileEnabled.ShouldBeTrue();
        person.DisablePublicProfile();
        person.DisablePublicProfile();
        person.PublicProfileEnabled.ShouldBeFalse();
    }

    [Fact]
    public void An_adult_shows_everything()
    {
        PublicExposure.Of(PersonStatus.Active, true, AgeStatus.Adult, _none).ShouldBe(new PublicExposure(true, true, true));
    }

    [Theory]
    [InlineData(AgeStatus.Minor)]
    [InlineData(AgeStatus.Unknown)]
    public void A_minor_shows_the_display_name_and_the_photo_only_with_photo_consent(AgeStatus ageStatus)
    {
        PublicExposure.Of(PersonStatus.Active, true, ageStatus, _none).ShouldBe(PublicExposure.Hidden);
        PublicExposure.Of(PersonStatus.Active, true, ageStatus, [ConsentScope.Photo]).ShouldBe(PublicExposure.Hidden);
        PublicExposure.Of(PersonStatus.Active, true, ageStatus, _publicProfile).ShouldBe(new PublicExposure(true, false, false));
        PublicExposure.Of(PersonStatus.Active, true, ageStatus, _publicProfileAndPhoto).ShouldBe(new PublicExposure(true, false, true));
    }

    [Theory]
    [InlineData(PersonStatus.Inactive)]
    [InlineData(PersonStatus.Archived)]
    [InlineData(PersonStatus.Deceased)]
    public void Only_an_active_person_with_the_profile_on_is_shown(PersonStatus status)
    {
        PublicExposure.Of(status, true, AgeStatus.Adult, _publicProfileAndPhoto).ShouldBe(PublicExposure.Hidden);
        PublicExposure.Of(PersonStatus.Active, false, AgeStatus.Adult, _publicProfileAndPhoto).ShouldBe(PublicExposure.Hidden);
    }

    private static Person Brenda() =>
        Person.Create(
            new PersonBiodata("Brenda", "Akello", DateOfBirth: new DateOnly(2011, 3, 2)), PersonSource.Manual, SystemActor.Instance, _now);

    private static GuardianConsentDetails Guardian(string name, GuardianRelationship relationship, ConsentScope scope) =>
        new(name, relationship, "+256 772 410 233", [scope], new DateOnly(2026, 11, 2));
}
