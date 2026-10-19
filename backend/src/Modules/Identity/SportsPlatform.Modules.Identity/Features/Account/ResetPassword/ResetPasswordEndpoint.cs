using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Account.ResetPassword;

internal static class ResetPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapPost("/password/reset", HandleAsync)
        .WithName("ResetPassword")
        .WithSummary("Set a new password with a reset token")
        .WithDescription(
            "Sets a new password with the token from a reset email's link. The token works once, for 1 hour, and only " +
            "the newest one works; otherwise `AUTH_RESET_TOKEN_INVALID`. The new password needs at least 12 " +
            "characters (`AUTH_PASSWORD_REJECTED`, the rule in `detail`). Signs out every session of the user and " +
            "clears a lockout; it does not sign in. Needs `X-CSRF: 1`.")
        .RequireAction(IdentityActions.PasswordReset)
        .ValidateRequest<ResetPasswordRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(IdentityErrors.ResetTokenInvalid, IdentityErrors.PasswordRejected, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ResetPasswordRequest request, ResetPasswordHandler handler, CancellationToken cancellationToken)
    {
        var failure = await handler.HandleAsync(request, cancellationToken);
        return failure is null ? TypedResults.NoContent() : failure.ToProblem("newPassword");
    }
}
