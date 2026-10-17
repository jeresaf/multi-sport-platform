namespace SportsPlatform.BuildingBlocks.Actors;

public enum ActorKind
{
    /// <summary>A signed-in person.</summary>
    User,

    /// <summary>Work with no user: CLI, seeds, outbox processor, scheduled jobs.</summary>
    System,
}
