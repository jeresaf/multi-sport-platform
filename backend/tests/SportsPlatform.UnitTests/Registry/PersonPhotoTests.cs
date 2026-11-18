using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

// S06 D9, D19: a photo is set on an active or inactive person, and removed in any status.
public sealed class PersonPhotoTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Setting_a_photo_returns_the_one_it_replaces()
    {
        var person = Person.Create(new PersonBiodata("Ivan", "Mukasa"), PersonSource.Manual, SystemActor.Instance, _now);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        person.SetPhoto(first).Value.ShouldBeNull();
        person.SetPhoto(second).Value.ShouldBe(first);

        person.PhotoAssetId.ShouldBe(second);
    }

    [Theory]
    [InlineData(PersonTransition.Deactivate, null)]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public void An_archived_or_deceased_person_takes_no_new_photo_but_loses_one(PersonTransition transition, string? refusal)
    {
        var person = Person.Create(new PersonBiodata("Ivan", "Mukasa"), PersonSource.Manual, SystemActor.Instance, _now);
        var asset = Guid.CreateVersion7();
        person.SetPhoto(asset).IsSuccess.ShouldBeTrue();
        person.ChangeStatus(transition, "Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        var set = person.SetPhoto(Guid.CreateVersion7());

        set.Error?.Code.ShouldBe(refusal);
        if (refusal is not null)
        {
            person.PhotoAssetId.ShouldBe(asset);
            person.RemovePhoto().Value.ShouldBe(asset);
            person.PhotoAssetId.ShouldBeNull();
        }
    }

    [Fact]
    public void Removing_without_a_photo_is_refused()
    {
        var person = Person.Create(new PersonBiodata("Ivan", "Mukasa"), PersonSource.Manual, SystemActor.Instance, _now);

        person.RemovePhoto().Error.ShouldBe(RegistryErrors.PhotoNotFound);
    }
}
