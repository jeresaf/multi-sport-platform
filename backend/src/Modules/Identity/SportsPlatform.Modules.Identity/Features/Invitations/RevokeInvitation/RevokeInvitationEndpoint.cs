using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Invitations.RevokeInvitation;

internal static class RevokeInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapPost("/{invitationId:guid}/revoke", HandleAsync)
        .WithName("RevokeInvitation")
        .WithSummary("Revoke an invitation")
        .WithDescription(
            "PENDING → REVOKED: the invitation's link stops working. An accepted, revoked or expired invitation can't be " +
            $"revoked. The body is optional. Returns the invitation. {InvitationEndpoints.VersionNote}")
        .RequireAction(IdentityActions.InvitationRevoke)
        .ProducesErrors(IdentityErrors.InvitationNotFound, IdentityErrors.InvitationTransitionInvalid, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<InvitationResponse>, ProblemHttpResult>> HandleAsync(
        Guid invitationId,
        RevokeInvitationRequest? request,
        RevokeInvitationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpContext.Request, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(invitationId, request?.Version, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(httpContext.Response);
        return TypedResults.Ok(result.Value);
    }
}
