using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class SystemActorTests
{
    [Fact]
    public void System_actor_has_a_fixed_non_empty_id()
    {
        SystemActor.Instance.Kind.ShouldBe(ActorKind.System);
        SystemActor.Instance.ActorId.ShouldBe(SystemActor.Id);
        SystemActor.Id.ShouldNotBe(Guid.Empty);
        SystemActor.Id.ShouldBe(new Guid("00000000-0000-7000-8000-000000000001"));
    }
}
