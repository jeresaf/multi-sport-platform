using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Members.GetMember;

internal static class GetMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder members) => members
        .MapGet("/{membershipId:guid}", HandleAsync)
        .WithName("GetMember")
        .WithSummary("Get a member")
        .WithDescription("A member of the tenant named by `X-Tenant-Id`, with their active roles and `version` (also the `ETag`).")
        .RequireAction(IdentityActions.MembershipRead)
        .ProducesErrors(IdentityErrors.MembershipNotFound);

    private static async Task<Results<Ok<MemberResponse>, ProblemHttpResult>> HandleAsync(
        Guid membershipId,
        GetMemberHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(membershipId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
