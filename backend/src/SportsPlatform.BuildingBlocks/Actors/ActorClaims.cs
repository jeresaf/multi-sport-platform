namespace SportsPlatform.BuildingBlocks.Actors;

/// <summary>The claims the Host reads the actor from; Identity issues them (S03 D5).</summary>
public static class ActorClaims
{
    /// <summary>The claim that carries an authenticated user's ID.</summary>
    public const string UserId = "sub";
}
