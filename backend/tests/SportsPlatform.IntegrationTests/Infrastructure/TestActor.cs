using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>A switchable actor so tests can tell who created a row from who last changed it.</summary>
public sealed class TestActor : ICurrentActor
{
    public ActorKind Kind { get; private set; } = ActorKind.System;

    public Guid ActorId { get; private set; } = SystemActor.Id;

    public void ActAsUser(Guid userId)
    {
        Kind = ActorKind.User;
        ActorId = userId;
    }

    public void ActAsSystem()
    {
        Kind = ActorKind.System;
        ActorId = SystemActor.Id;
    }
}
