using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

internal static class ForgotPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapPost("/password/forgot", HandleAsync)
        .WithName("ForgotPassword")
        .WithSummary("Ask for a password reset email")
        .WithDescription(
            "Sends a password reset link to the email if it belongs to an account. Always answers 202, whether or not " +
            "it does. The link works once, for 1 hour, and only the newest link works. Limited to 10 requests an hour " +
            "per client address and 3 an hour per email (`AUTH_RATE_LIMITED`, with `Retry-After`). Needs `X-CSRF: 1`.")
        .RequireAction(IdentityActions.PasswordForgot)
        .ValidateRequest<ForgotPasswordRequest>(CommonErrors.InvalidRequest)
        .RateLimited<ForgotPasswordRequest>(AccountRateLimit.ForgotPassword, request => request.Email)
        .Produces(StatusCodes.Status202Accepted);

    private static async Task<Accepted> HandleAsync(
        ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }
}
