namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>
/// The scoped <see cref="ICurrentActor"/>: the system actor until set. The Host sets it once per request from the
/// authenticated principal (S02 D5); work outside a request (consumers, seeds, jobs) keeps the system actor.
/// </summary>
public sealed class ActorContext : ICurrentActor
{
    private ICurrentActor _actor = SystemActor.Instance;
    private bool _isSet;

    public ActorKind Kind => _actor.Kind;

    public Guid ActorId => _actor.ActorId;

    /// <summary>Sets the scope's actor. Setting the same actor again is a no-op; a different one is a bug and throws.</summary>
    public void Set(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (_isSet && (_actor.Kind, _actor.ActorId) != (actor.Kind, actor.ActorId))
        {
            throw new InvalidOperationException("The actor of this scope is already set to another actor; a scope runs as one actor.");
        }

        _actor = actor;
        _isSet = true;
    }
}
