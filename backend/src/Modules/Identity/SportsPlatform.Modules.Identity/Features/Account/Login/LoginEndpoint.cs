using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

namespace SportsPlatform.Modules.Identity.Features.Account.Login;

internal static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapPost("/login", HandleAsync)
        .WithName("Login")
        .WithSummary("Sign in")
        .WithDescription(
            "Signs in with email and password and starts a session: the response sets the session cookie (HttpOnly, " +
            "SameSite=Strict; 12 hours sliding, 7 days at most). An unknown email, a wrong password, a locked or " +
            "unconfirmed account all answer `AUTH_INVALID_CREDENTIALS`. Five wrong passwords lock the account for 15 " +
            "minutes. Limited to 20 attempts a minute per client address and 10 per 15 minutes per email " +
            "(`AUTH_RATE_LIMITED`, with `Retry-After`). Needs `X-CSRF: 1` like every other change.")
        .RequireAction(IdentityActions.AccountLogin)
        .ValidateRequest<LoginRequest>(CommonErrors.InvalidRequest)
        .RateLimited<LoginRequest>(AccountRateLimit.Login, request => request.Email)
        .ProducesErrors(IdentityErrors.InvalidCredentials);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        LoginRequest request, LoginHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.NoContent();
    }
}
