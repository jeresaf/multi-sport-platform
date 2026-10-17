namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>Who is performing the current work; feeds audit columns and audit entries.</summary>
public interface ICurrentActor
{
    ActorKind Kind { get; }

    Guid ActorId { get; }
}
