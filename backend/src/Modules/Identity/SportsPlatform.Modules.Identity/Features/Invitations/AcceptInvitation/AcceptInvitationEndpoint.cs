using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

namespace SportsPlatform.Modules.Identity.Features.Invitations.AcceptInvitation;

internal static class AcceptInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapPost("/accept", HandleAsync)
        .WithName("AcceptInvitation")
        .WithSummary("Accept an invitation")
        .WithDescription(
            "Joins the invitation's tenant with its roles. If the invited email has no account, send `password` (at " +
            "least 12 characters, `AUTH_PASSWORD_REJECTED`): the account is created with that password. If it has one, " +
            "sign in with it first (`AUTH_INVITATION_SIGN_IN_REQUIRED` otherwise); `password` is then ignored. The token " +
            "works once and the same refusals as the lookup apply; a current member gets `AUTH_ALREADY_MEMBER`. Does " +
            "not sign in. Anonymous callers allowed; needs `X-CSRF: 1`.")
        .RequireAction(IdentityActions.InvitationAccept)
        .ValidateRequest<AcceptInvitationRequest>(CommonErrors.InvalidRequest)
        .RateLimitedPerIp(AccountRateLimit.Invitation)
        .ProducesErrors(
            IdentityErrors.InvitationTokenInvalid, IdentityErrors.InvitationExpired, IdentityErrors.InvitationAlreadyAccepted,
            IdentityErrors.InvitationSignInRequired, IdentityErrors.PasswordRejected, IdentityErrors.RoleNotAssignable,
            IdentityErrors.AlreadyMember, IdentityErrors.TenantArchived, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<AcceptInvitationResponse>, ProblemHttpResult>> HandleAsync(
        AcceptInvitationRequest request, AcceptInvitationHandler handler, CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(request, cancellationToken);
        return outcome.Accepted is { } accepted ? TypedResults.Ok(accepted) : outcome.Failure!.ToProblem("password");
    }
}
