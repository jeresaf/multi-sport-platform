namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>
/// The actor of a request from a caller who is not signed in (S03 D3): sign-in, password reset, public reads and
/// refused requests. Work outside a request keeps the <see cref="SystemActor"/>.
/// </summary>
public sealed class AnonymousActor : ICurrentActor
{
    /// <summary>Fixed ID recorded in audit entries and audit columns for anonymous requests.</summary>
    public static readonly Guid Id = Guid.Empty;

    public static AnonymousActor Instance { get; } = new();

    private AnonymousActor()
    {
    }

    public ActorKind Kind => ActorKind.Anonymous;

    public Guid ActorId => Id;
}
