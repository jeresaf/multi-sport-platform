using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Account.ChangePassword;

internal static class ChangePasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapPost("/password/change", HandleAsync)
        .WithName("ChangePassword")
        .WithSummary("Change the password")
        .WithDescription(
            "Changes the signed-in user's password. A wrong current password answers `AUTH_CURRENT_PASSWORD_INVALID` " +
            "and counts toward the lockout (five lock the account for 15 minutes). The new password needs at least 12 " +
            "characters (`AUTH_PASSWORD_REJECTED`, the rule in `detail`). The user's other sessions end within 5 " +
            "minutes; this one stays (the response renews its cookie). Needs `X-CSRF: 1`.")
        .RequireAction(IdentityActions.PasswordChange)
        .ValidateRequest<ChangePasswordRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(IdentityErrors.CurrentPasswordInvalid, IdentityErrors.PasswordRejected, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ChangePasswordRequest request, ChangePasswordHandler handler, CancellationToken cancellationToken)
    {
        var failure = await handler.HandleAsync(request, cancellationToken);
        return failure is null ? TypedResults.NoContent() : failure.ToProblem("newPassword");
    }
}
