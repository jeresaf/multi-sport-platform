using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.Host.Actors;

public static class CurrentActor
{
    /// <summary>The claim that carries an authenticated user's ID.</summary>
    public const string UserIdClaim = ActorClaims.UserId;

    /// <summary>The scoped actor, the system actor unless a request sets it. Shared by the API and the CLI.</summary>
    public static IServiceCollection AddCurrentActor(this IServiceCollection services)
    {
        services.AddScoped<ActorContext>();
        services.AddScoped<ICurrentActor>(provider => provider.GetRequiredService<ActorContext>());
        return services;
    }

    /// <summary>Authenticates (the session cookie, S03 D5) and sets the actor.</summary>
    public static IApplicationBuilder UseCurrentActor(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        return app.UseMiddleware<CurrentActorMiddleware>();
    }
}
