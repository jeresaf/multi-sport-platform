using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteOwner;

/// <summary><c>POST /api/platform/v1/tenants/{tenantId}/owner-invitations</c>: the platform console invites a tenant's owner.</summary>
internal static class InviteOwnerEndpoint
{
    public static void Map(IEndpointRouteBuilder platform) => platform
        .MapPost("/tenants/{tenantId:guid}/owner-invitations", HandleAsync)
        .WithTags("Tenants")
        .WithName("InviteTenantOwner")
        .WithSummary("Invite a tenant's owner")
        .WithDescription(
            "Invites `email` to join the tenant as TENANT_OWNER, usually while it is PROVISIONING; an ARCHIVED tenant is refused. " +
            $"A person who is already a member can't be invited. {InvitationEndpoints.InviteNote}")
        .RequireAction(IdentityActions.TenantOwnerInvite)
        .ReturnsETag()
        .RunsUnderRouteTenant()
        .ValidateRequest<InviteOwnerRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(IdentityErrors.TenantArchived, IdentityErrors.AlreadyMember, IdentityErrors.ConcurrencyConflict);

    private static async Task<Results<Created<InvitationResponse>, ProblemHttpResult>> HandleAsync(
        Guid tenantId, InviteOwnerRequest request, InviteHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.InviteOwnerAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // The invitation is read in the tenant's admin API, which platform staff don't use: no Location.
        result.Value.SetETag(response);
        return TypedResults.Created((string?)null, result.Value);
    }
}
