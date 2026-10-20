using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Members.ListMembers;

internal static class ListMembersEndpoint
{
    public static void Map(IEndpointRouteBuilder members) => members
        .MapGet("", HandleAsync)
        .WithName("ListMembers")
        .WithSummary("List the tenant's members")
        .WithDescription(
            "The members of the tenant named by `X-Tenant-Id`, oldest first, with their email and active roles. Filter by " +
            "`status` (ACTIVE, SUSPENDED or REMOVED); without it, removed members are left out. `limit` is 1–100 (25). " +
            "Every page read is audited.")
        .RequireAction(IdentityActions.MembershipRead)
        .ValidateRequest<ListMembersRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<ListMembersResponse>> HandleAsync(
        [AsParameters] ListMembersRequest request,
        ListMembersHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
