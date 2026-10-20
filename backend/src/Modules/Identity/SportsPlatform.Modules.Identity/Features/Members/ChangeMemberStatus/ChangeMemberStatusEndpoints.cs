using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Members.ChangeMemberStatus;

/// <summary><c>POST /members/{membershipId}/{suspend|restore|remove}</c>: one action each.</summary>
internal static class ChangeMemberStatusEndpoints
{
    private const string _reasonNote =
        "The reason is kept permanently on the membership and in the audit trail: don't include personal data.";

    private static readonly Error[] _errors =
    [
        IdentityErrors.MembershipNotFound, IdentityErrors.MembershipTransitionInvalid, IdentityErrors.OwnerRequired,
        IdentityErrors.LastOwner, IdentityErrors.ConcurrencyConflict,
    ];

    public static void Map(IEndpointRouteBuilder members)
    {
        MapWithReason(members, "suspend", MemberTransition.Suspend, IdentityActions.MembershipSuspend, "SuspendMember",
            "Suspend a member", "ACTIVE → SUSPENDED, with a reason: the member loses access to the tenant and keeps their roles.");
        members
            .MapPost("/{membershipId:guid}/restore", (
                    Guid membershipId,
                    ChangeMemberStatusRequest? request,
                    ChangeMemberStatusHandler handler,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                HandleAsync(membershipId, MemberTransition.Restore, null, request?.Version, handler, httpContext, cancellationToken))
            .WithName("RestoreMember")
            .WithSummary("Restore a suspended member")
            .WithDescription($"SUSPENDED → ACTIVE, with the roles the member held. The body is optional. {MemberEndpoints.VersionNote}")
            .RequireAction(IdentityActions.MembershipRestore)
            .ProducesErrors(_errors);
        MapWithReason(members, "remove", MemberTransition.Remove, IdentityActions.MembershipRemove, "RemoveMember",
            "Remove a member", "ACTIVE or SUSPENDED → REMOVED, with a reason. Final: rejoining needs a new invitation.");
    }

    private static void MapWithReason(
        IEndpointRouteBuilder members, string path, MemberTransition transition, ActionDefinition action, string name, string summary,
        string description) => members
        .MapPost($"/{{membershipId:guid}}/{path}", (
                Guid membershipId,
                ChangeMemberStatusWithReasonRequest request,
                ChangeMemberStatusHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            HandleAsync(membershipId, transition, request.Reason!.Trim(), request.Version, handler, httpContext, cancellationToken))
        .WithName(name)
        .WithSummary(summary)
        .WithDescription($"{description} Only an owner changes an owner, and the last active owner can't be suspended or removed. " +
            $"The body `{{ \"reason\": … }}` is required. {_reasonNote} {MemberEndpoints.VersionNote}")
        .RequireAction(action)
        .ValidateRequest<ChangeMemberStatusWithReasonRequest>(CommonErrors.ReasonRequired)
        .ProducesErrors(_errors);

    private static async Task<Results<Ok<MemberResponse>, ProblemHttpResult>> HandleAsync(
        Guid membershipId,
        MemberTransition transition,
        string? reason,
        uint? version,
        ChangeMemberStatusHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpContext.Request, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(membershipId, transition, reason, version, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(httpContext.Response);
        return TypedResults.Ok(result.Value);
    }
}
