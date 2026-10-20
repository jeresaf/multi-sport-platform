using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Members.RevokeRole;

internal static class RevokeRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder members) => members
        .MapPost("/{membershipId:guid}/roles/{assignmentId:guid}/revoke", HandleAsync)
        .WithName("RevokeRole")
        .WithSummary("Revoke a member's role")
        .WithDescription(
            "Revokes an active role assignment of the member; it is kept as history. Only an owner revokes TENANT_OWNER, " +
            $"and the last active owner keeps it. The body is optional. Returns the member. {MemberEndpoints.VersionNote}")
        .RequireAction(IdentityActions.RoleRevoke)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            IdentityErrors.MembershipNotFound, IdentityErrors.RoleAssignmentNotFound, IdentityErrors.MembershipTransitionInvalid,
            IdentityErrors.OwnerRequired, IdentityErrors.LastOwner, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<MemberResponse>, ProblemHttpResult>> HandleAsync(
        Guid membershipId,
        Guid assignmentId,
        RevokeRoleRequest? request,
        RevokeRoleHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpContext.Request, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(membershipId, assignmentId, request?.Version, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(httpContext.Response);
        return TypedResults.Ok(result.Value);
    }
}
