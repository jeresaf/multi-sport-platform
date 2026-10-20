using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;

internal static class InviteMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapPost("", HandleAsync)
        .WithName("InviteMember")
        .WithSummary("Invite a person to the tenant")
        .WithDescription(
            "Invites `email` to join the tenant named by `X-Tenant-Id` with `roleCodes` (tenant roles, tenant-wide). Only an " +
            $"owner invites with TENANT_OWNER. A person who is already a member (active or suspended) can't be invited. {InvitationEndpoints.InviteNote}")
        .RequireAction(IdentityActions.MembershipInvite)
        .ValidateRequest<InviteMemberRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            IdentityErrors.RoleNotAssignable, IdentityErrors.OwnerRequired, IdentityErrors.AlreadyMember, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Created<InvitationResponse>, ProblemHttpResult>> HandleAsync(
        InviteMemberRequest request, InviteHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.InviteMemberAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Created(InvitationEndpoints.PathOf(result.Value.InvitationId), result.Value);
    }
}
