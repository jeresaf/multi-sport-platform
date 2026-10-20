using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Invitations.GetInvitation;

internal static class GetInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapGet("/{invitationId:guid}", HandleAsync)
        .WithName("GetInvitation")
        .WithSummary("Get an invitation")
        .WithDescription("An invitation of the tenant named by `X-Tenant-Id`, with its email, roles, status and `version` (also the `ETag`).")
        .RequireAction(IdentityActions.MembershipRead)
        .ReturnsETag()
        .ProducesErrors(IdentityErrors.InvitationNotFound);

    private static async Task<Results<Ok<InvitationResponse>, ProblemHttpResult>> HandleAsync(
        Guid invitationId, GetInvitationHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(invitationId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
