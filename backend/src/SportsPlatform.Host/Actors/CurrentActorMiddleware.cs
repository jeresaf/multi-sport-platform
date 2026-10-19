using System.Security.Claims;
using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.Host.Actors;

/// <summary>
/// Sets the request's actor from the authenticated principal (S02 D5): a user for a principal with a user ID
/// (<c>sub</c>), else the anonymous actor (S03 D3), which the pipeline answers with 401 on any action that needs a
/// user (D4). An authenticated principal without a GUID <c>sub</c> is anonymous too. Work outside a request keeps the
/// system actor.
/// </summary>
internal sealed class CurrentActorMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ICurrentActor actor = context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(CurrentActor.UserIdClaim), out var userId)
            && userId != Guid.Empty
                ? new UserActor(userId)
                : AnonymousActor.Instance;
        context.RequestServices.GetRequiredService<ActorContext>().Set(actor);

        return next(context);
    }
}
