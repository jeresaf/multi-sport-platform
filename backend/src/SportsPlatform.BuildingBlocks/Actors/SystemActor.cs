namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>The actor for work with no user, so audit columns are never empty.</summary>
public sealed class SystemActor : ICurrentActor
{
    /// <summary>Fixed ID recorded in <c>created_by</c>/<c>updated_by</c> and audit entries for system work.</summary>
    public static readonly Guid Id = new("00000000-0000-7000-8000-000000000001");

    public static SystemActor Instance { get; } = new();

    private SystemActor()
    {
    }

    public ActorKind Kind => ActorKind.System;

    public Guid ActorId => Id;
}
