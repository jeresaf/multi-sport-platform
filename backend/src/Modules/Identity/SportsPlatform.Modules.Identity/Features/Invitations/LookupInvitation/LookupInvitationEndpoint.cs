using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;

namespace SportsPlatform.Modules.Identity.Features.Invitations.LookupInvitation;

internal static class LookupInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapPost("/lookup", HandleAsync)
        .WithName("LookupInvitation")
        .WithSummary("Look up an invitation by its token")
        .WithDescription(
            "What an invitation link is for: the tenant, the invited email and roles, when it expires, and whether the " +
            "email has an account (then sign in with it before accepting; otherwise choose a password when accepting). " +
            "The token from the email's link goes in the body. An unknown, revoked or replaced token is " +
            "`AUTH_INVITATION_TOKEN_INVALID`; an expired invitation `AUTH_INVITATION_EXPIRED`; an accepted one " +
            "`AUTH_INVITATION_ALREADY_ACCEPTED`. Anonymous callers allowed; needs `X-CSRF: 1`.")
        .RequireAction(IdentityActions.InvitationRead)
        .ValidateRequest<LookupInvitationRequest>(CommonErrors.InvalidRequest)
        .RateLimitedPerIp(AccountRateLimit.Invitation)
        .ProducesErrors(
            IdentityErrors.InvitationTokenInvalid, IdentityErrors.InvitationExpired, IdentityErrors.InvitationAlreadyAccepted,
            IdentityErrors.TenantArchived);

    private static async Task<Results<Ok<LookupInvitationResponse>, ProblemHttpResult>> HandleAsync(
        LookupInvitationRequest request, LookupInvitationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
