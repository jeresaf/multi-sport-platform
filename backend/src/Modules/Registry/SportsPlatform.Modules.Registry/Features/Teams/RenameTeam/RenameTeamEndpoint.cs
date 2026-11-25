using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.RenameTeam;

/// <summary><c>POST /teams/{teamId}/rename</c> (S07 D8).</summary>
internal static class RenameTeamEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapPost("/{teamId:guid}/rename", (
                Guid teamId,
                RenameTeamRequest request,
                RenameTeamHandler handler,
                GetTeamHandler read,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                ifMatch => handler.HandleAsync(teamId, request, ifMatch, cancellationToken)))
        .WithName("RenameTeam")
        .WithSummary("Rename a team")
        .WithDescription(
            "Gives a team of the tenant named by `X-Tenant-Id` a new `name` from `effectiveOn`, which must be after the current " +
            "name's first day (`nameSince`) and not after today, the tenant's date (TEAM_RENAME_DATE_INVALID). The old name, " +
            "with the short name and code it had, is kept as a former name from its first day to the day before " +
            "`effectiveOn`: results of that time show it. `shortName` and `code` are the values from `effectiveOn`: left out, " +
            "the current one is kept; sent as null, it is cleared. The same name exactly is TEAM_NAME_UNCHANGED; a name " +
            "without a letter or digit TEAM_NAME_INVALID. The slug doesn't change. A disbanded or archived team can't be " +
            "renamed (TEAM_DISBANDED, TEAM_ARCHIVED). When the name or short name changes as the duplicate check compares it " +
            "(case, accents, punctuation and word order aside), the check runs again, the team itself left out: teams that " +
            "look like the renamed one answer TEAM_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request says " +
            "`duplicatesReviewed: true`, with an optional `duplicateReason`; the audit entry names the teams shown. The reason " +
            "is kept permanently in the audit trail: don't include personal data. Send `version` or `If-Match` from the last " +
            "read to get 409 TEAM_CONCURRENCY_CONFLICT if the team changed since. Answers the team as GET /teams/{teamId} " +
            "does; its version moves.")
        .RequireAction(RegistryActions.TeamRename)
        .ValidateRequest<RenameTeamRequest>(CommonErrors.InvalidRequest)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamNameInvalid,
            RegistryErrors.TeamNameUnchanged, RegistryErrors.TeamRenameDateInvalid, RegistryErrors.TeamArchived,
            RegistryErrors.TeamDisbanded, RegistryErrors.TeamDuplicateReviewRequired)
        .ProducesProblem<TeamDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);
}
