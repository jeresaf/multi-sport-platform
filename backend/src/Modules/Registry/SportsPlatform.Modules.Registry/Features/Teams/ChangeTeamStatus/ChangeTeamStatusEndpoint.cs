using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.ChangeTeamStatus;

internal static class ChangeTeamStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapPost("/{teamId:guid}/status", HandleAsync)
        .WithName("ChangeTeamStatus")
        .WithSummary("Change a team's status")
        .WithDescription(
            "Moves the team, with a reason (body `{ \"status\": …, \"reason\": … }`, both required): ACTIVE, INACTIVE and " +
            "SUSPENDED move between each other and to DISBANDED or ARCHIVED; DISBANDED → ACTIVE (reform) or ARCHIVED; " +
            "ARCHIVED → the status it had before archiving (unarchive), and no other. Any other move is " +
            "TEAM_STATUS_INVALID. Teams in ACTIVE, INACTIVE or SUSPENDED count toward the plan's MAX_TEAMS: disbanding or " +
            "archiving frees a place, and reforming or unarchiving to one of them needs a free place " +
            "(ENTITLEMENT_QUOTA_EXCEEDED otherwise). A team whose sport is no longer enabled still changes status. Nothing " +
            "is deleted: each move is kept in the team's history (GET /teams/{teamId}/status-changes). The reason is kept " +
            "permanently in that history and the audit trail: don't include personal data. Send `version` or `If-Match` " +
            "from the last read to get 409 TEAM_CONCURRENCY_CONFLICT if the team changed since. Answers the team as " +
            "GET /teams/{teamId} does.")
        .RequireAction(RegistryActions.TeamStatusChange)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<ChangeTeamStatusRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            CommonErrors.ReasonRequired, RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict,
            RegistryErrors.TeamStatusInvalid);

    private static async Task<Results<Ok<TeamResponse>, ProblemHttpResult>> HandleAsync(
        Guid teamId,
        ChangeTeamStatusRequest request,
        ChangeTeamStatusHandler handler,
        GetTeamHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await handler.HandleAsync(teamId, request, ifMatch, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var team = await read.HandleAsync(teamId, CancellationToken.None);
        team.Value.SetETag(response);
        return TypedResults.Ok(team.Value);
    }
}
