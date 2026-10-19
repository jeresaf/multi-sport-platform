using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class AnonymousActorTests
{
    // S03 D3: a fixed ID, distinct from the system actor's.
    [Fact]
    public void Anonymous_actor_has_the_empty_id()
    {
        AnonymousActor.Instance.Kind.ShouldBe(ActorKind.Anonymous);
        AnonymousActor.Instance.ActorId.ShouldBe(Guid.Empty);
        AnonymousActor.Id.ShouldNotBe(SystemActor.Id);
    }

    [Fact]
    public void A_request_scope_set_to_anonymous_cannot_become_a_user()
    {
        var context = new ActorContext();
        context.Set(AnonymousActor.Instance);

        Should.Throw<InvalidOperationException>(() => context.Set(new UserActor(Guid.CreateVersion7())));
        context.Kind.ShouldBe(ActorKind.Anonymous);
    }
}
