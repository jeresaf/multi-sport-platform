using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.Host.Actors;

public static class CurrentActor
{
    /// <summary>The claim that carries an authenticated user's ID.</summary>
    public const string UserIdClaim = "sub";

    /// <summary>The scoped actor, the system actor unless a request sets it. Shared by the API and the CLI.</summary>
    public static IServiceCollection AddCurrentActor(this IServiceCollection services)
    {
        services.AddScoped<ActorContext>();
        services.AddScoped<ICurrentActor>(provider => provider.GetRequiredService<ActorContext>());
        return services;
    }

    /// <summary>Authenticates (no scheme before S03; the Testing environment adds one) and sets the actor.</summary>
    public static IApplicationBuilder UseCurrentActor(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        return app.UseMiddleware<CurrentActorMiddleware>();
    }
}
