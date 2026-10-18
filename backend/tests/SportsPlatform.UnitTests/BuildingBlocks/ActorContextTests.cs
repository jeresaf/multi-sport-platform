using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>A scope runs as one actor (S02 D5): the system actor unless the request sets a user.</summary>
public sealed class ActorContextTests
{
    private static readonly Guid _secretary = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _registrar = new("0192a1b2-0000-7000-8000-00000000c002");

    [Fact]
    public void Is_the_system_actor_until_set()
    {
        var context = new ActorContext();

        context.Kind.ShouldBe(ActorKind.System);
        context.ActorId.ShouldBe(SystemActor.Id);
    }

    [Fact]
    public void Setting_the_same_actor_again_is_a_no_op()
    {
        var context = new ActorContext();

        context.Set(new UserActor(_secretary));
        context.Set(new UserActor(_secretary));

        context.Kind.ShouldBe(ActorKind.User);
        context.ActorId.ShouldBe(_secretary);
    }

    [Fact]
    public void Another_actor_after_one_is_set_throws()
    {
        var context = new ActorContext();
        context.Set(new UserActor(_secretary));

        Should.Throw<InvalidOperationException>(() => context.Set(new UserActor(_registrar)));
        Should.Throw<InvalidOperationException>(() => context.Set(SystemActor.Instance));
        context.ActorId.ShouldBe(_secretary);
    }
}
