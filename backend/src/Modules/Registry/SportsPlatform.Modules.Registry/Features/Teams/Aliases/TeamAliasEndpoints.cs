using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.Aliases;

/// <summary><c>POST /teams/{teamId}/aliases</c> and <c>POST /teams/{teamId}/aliases/{aliasId}/remove</c> (S07 D8, D10).</summary>
internal static class TeamAliasEndpoints
{
    public const string VersionNote =
        "Send `version` or `If-Match` from the last read to get 409 TEAM_CONCURRENCY_CONFLICT if the team changed since; " +
        "without them, a change to the same team saved at the same moment can still answer it. " +
        "Answers the team as GET /teams/{teamId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder teams)
    {
        teams
            .MapPost("/{teamId:guid}/aliases", (
                    Guid teamId,
                    AddTeamAliasRequest request,
                    AddTeamAliasHandler handler,
                    GetTeamHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(teamId, request, ifMatch, cancellationToken)))
            .WithName("AddTeamAlias")
            .WithSummary("Add a nickname or spelling to a team")
            .WithDescription(
                "Adds another name the team is known by (`NICKNAME` or `SPELLING`; a former name comes from POST " +
                "/teams/{teamId}/rename): the directory search finds the team under it too. Adding one runs no duplicate " +
                "warning. A name the team is already known by (the same name key as its name or short name or a current " +
                "alias, former names included: word order, case, accents and punctuation aside) is TEAM_ALIAS_EXISTS; a name " +
                "without a letter or a digit TEAM_NAME_INVALID. A disbanded or archived team takes no new alias " +
                $"(TEAM_DISBANDED, TEAM_ARCHIVED). {VersionNote}")
            .RequireAction(RegistryActions.TeamAliasAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddTeamAliasRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamNameInvalid,
                RegistryErrors.TeamAliasExists, RegistryErrors.TeamArchived, RegistryErrors.TeamDisbanded);

        teams
            .MapPost("/{teamId:guid}/aliases/{aliasId:guid}/remove", (
                    Guid teamId,
                    Guid aliasId,
                    RemoveTeamAliasRequest request,
                    RemoveTeamAliasHandler handler,
                    GetTeamHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(teamId, aliasId, request, ifMatch, cancellationToken)))
            .WithName("RemoveTeamAlias")
            .WithSummary("Remove a team's nickname or spelling")
            .WithDescription(
                "Removes a nickname or spelling, whatever the team's status, with a reason (body `{ \"reason\": … }`, required): " +
                "the alias is kept with who removed it, when and why, and is no longer shown or searched. The reason is kept " +
                "permanently on the alias and in the audit trail: don't include personal data. A former name is the team's " +
                "name history and can't be removed (TEAM_ALIAS_FORMER_NAME); an alias already removed is TEAM_ALIAS_REMOVED. " +
                VersionNote)
            .RequireAction(RegistryActions.TeamAliasRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<RemoveTeamAliasRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(
                RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamAliasNotFound,
                RegistryErrors.TeamAliasFormerName, RegistryErrors.TeamAliasRemoved);
    }
}
