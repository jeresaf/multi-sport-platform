using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Invitations.ListInvitations;

internal static class ListInvitationsEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapGet("", HandleAsync)
        .WithName("ListInvitations")
        .WithSummary("List the tenant's invitations")
        .WithDescription(
            "The invitations of the tenant named by `X-Tenant-Id`, oldest first, with their email, roles and status. Filter by " +
            "`status` (PENDING, EXPIRED, ACCEPTED or REVOKED); without it, the ones still pending, expired ones included. " +
            "`limit` is 1–100 (25). Every page read is audited.")
        .RequireAction(IdentityActions.MembershipRead)
        .ValidateRequest<ListInvitationsRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<ListInvitationsResponse>> HandleAsync(
        [AsParameters] ListInvitationsRequest request,
        ListInvitationsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
