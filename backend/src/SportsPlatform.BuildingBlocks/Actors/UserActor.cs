namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>An authenticated user.</summary>
public sealed record UserActor(Guid ActorId) : ICurrentActor
{
    public ActorKind Kind => ActorKind.User;
}
