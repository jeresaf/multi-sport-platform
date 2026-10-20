using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Members.AssignRole;

internal static class AssignRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder members) => members
        .MapPost("/{membershipId:guid}/roles", HandleAsync)
        .WithName("AssignRole")
        .WithSummary("Give a member a role")
        .WithDescription(
            "Assigns a tenant role (`roleCode`, e.g. REGISTRAR) to an active or suspended member, tenant-wide: `scopeType` " +
            "is TENANT (the default) until competitions, teams and contests exist. Only an owner grants TENANT_OWNER. " +
            $"Returns the member. {MemberEndpoints.VersionNote}")
        .RequireAction(IdentityActions.RoleAssign)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<AssignRoleRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            IdentityErrors.MembershipNotFound, IdentityErrors.RoleNotAssignable, IdentityErrors.RoleAlreadyAssigned,
            IdentityErrors.MembershipTransitionInvalid, IdentityErrors.OwnerRequired, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Created<MemberResponse>, ProblemHttpResult>> HandleAsync(
        Guid membershipId,
        AssignRoleRequest request,
        AssignRoleHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpContext.Request, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(membershipId, request, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(httpContext.Response);
        return TypedResults.Created(MemberEndpoints.PathOf(membershipId), result.Value);
    }
}
