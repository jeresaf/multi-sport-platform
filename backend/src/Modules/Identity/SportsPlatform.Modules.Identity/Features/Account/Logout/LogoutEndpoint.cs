using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.Logout;

/// <summary>
/// Ends this session: the response deletes the cookie. A copy of the cookie kept elsewhere stays valid until it expires
/// or the security stamp changes (password change or reset); the cookie is not tracked server-side.
/// </summary>
internal static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapPost("/logout", HandleAsync)
        .WithName("Logout")
        .WithSummary("Sign out")
        .WithDescription("Ends this session and deletes the session cookie.")
        .RequireAction(IdentityActions.AccountLogout);

    private static async Task<NoContent> HandleAsync(SignInManager<User> signIn, ICurrentActor actor, AuditScope audit)
    {
        audit.Describe(AccountEndpoints.UserEntityType, actor.ActorId);
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }
}
