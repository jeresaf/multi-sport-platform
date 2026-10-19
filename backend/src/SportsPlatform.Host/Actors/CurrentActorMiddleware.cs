using System.Security.Claims;
using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.Host.Actors;

/// <summary>
/// Sets the request's actor from the authenticated principal (S02 D5): a user for a principal with a user ID
/// (<c>sub</c>), else the system actor until S03 adds the anonymous actor. Nothing authenticates outside tests in S02.
/// </summary>
internal sealed class CurrentActorMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(CurrentActor.UserIdClaim), out var userId))
        {
            context.RequestServices.GetRequiredService<ActorContext>().Set(new UserActor(userId));
        }

        return next(context);
    }
}
