using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;

internal static class InviteMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) => invitations
        .MapPost("", HandleAsync)
        .WithName("InviteMember")
        .WithSummary("Invite a person to the tenant")
        .WithDescription(
            "Invites `email` to join the tenant named by `X-Tenant-Id` with `roleCodes` (tenant roles, tenant-wide). Only an " +
            "owner invites with TENANT_OWNER or replaces a pending invitation that has it. A person who is already a member (active or suspended) can't be invited. " +
            "Every member who isn't removed and every pending invitation takes one of the plan's staff users (MAX_STAFF_USERS); an invitation past the limit is " +
            $"ENTITLEMENT_QUOTA_EXCEEDED, and one that replaces a pending invitation to the same email adds no one. {InvitationEndpoints.InviteNote}")
        .RequireAction(IdentityActions.MembershipInvite)
        .ReturnsETag()
        .ValidateRequest<InviteMemberRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            IdentityErrors.RoleNotAssignable,
            IdentityErrors.OwnerRequired,
            IdentityErrors.AlreadyMember,
            IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Created<InvitationResponse>, ProblemHttpResult>> HandleAsync(
        InviteMemberRequest request,
        InviteHandler handler,
        IEntitlements entitlements,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.InviteMemberAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            // A Result carries no detail (S04-T3a2): the limit is read again for the message. The Host documents the
            // error, as for every action that names a quota.
            return result.Error.Code == EntitlementErrorCodes.QuotaExceeded
                ? result.Error.ToProblem(
                    $"Your plan allows {await entitlements.GetLimitAsync(StaffUsers.Code, cancellationToken)} staff users.")
                : result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Created(InvitationEndpoints.PathOf(result.Value.InvitationId), result.Value);
    }
}
