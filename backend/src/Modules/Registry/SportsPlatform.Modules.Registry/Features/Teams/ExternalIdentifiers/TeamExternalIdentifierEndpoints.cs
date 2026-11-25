using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.Aliases;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.ExternalIdentifiers;

/// <summary>
/// <c>POST /teams/{teamId}/external-identifiers</c> and <c>POST /teams/{teamId}/external-identifiers/{identifierId}/end</c>
/// (S07 D10).
/// </summary>
internal static class TeamExternalIdentifierEndpoints
{
    public static void Map(IEndpointRouteBuilder teams)
    {
        teams
            .MapPost("/{teamId:guid}/external-identifiers", (
                    Guid teamId,
                    AddTeamExternalIdentifierRequest request,
                    AddTeamExternalIdentifierHandler handler,
                    GetTeamHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(teamId, request, ifMatch, cancellationToken)))
            .WithName("AddTeamExternalIdentifier")
            .WithSummary("Add an external identifier to a team")
            .WithDescription(
                "Adds an identifier another authority gave the team: `authority` is a code the tenant chooses (e.g. " +
                "`KMSA_TEAM_NO`), `value` is trimmed. A value with this authority that any team has or had, ended ones and " +
                "this team's included, is TEAM_EXTERNAL_ID_EXISTS: a value is never reused (a person's, venue's or " +
                "organisation's identifier with the same authority and value doesn't count). The directory finds the team by " +
                "the exact value. A disbanded or archived team takes no new identifier (TEAM_DISBANDED, TEAM_ARCHIVED). " +
                TeamAliasEndpoints.VersionNote)
            .RequireAction(RegistryActions.TeamExternalIdAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddTeamExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamExternalIdExists,
                RegistryErrors.TeamArchived, RegistryErrors.TeamDisbanded);

        teams
            .MapPost("/{teamId:guid}/external-identifiers/{identifierId:guid}/end", (
                    Guid teamId,
                    Guid identifierId,
                    EndTeamExternalIdentifierRequest? request,
                    EndTeamExternalIdentifierHandler handler,
                    GetTeamHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(teamId, identifierId, request ?? new(), ifMatch, cancellationToken)))
            .WithName("EndTeamExternalIdentifier")
            .WithSummary("End a team's external identifier")
            .WithDescription(
                "Ends an identifier, whatever the team's status: it is kept with its `validTo`, and its value is never given to " +
                "another team. The body is optional: `validTo` defaults to today (the tenant's date) and can't be after it or " +
                "before `validFrom` (TEAM_EXTERNAL_ID_VALIDITY_INVALID); `reason` (1–500 characters) is kept permanently in the " +
                "audit trail: don't include personal data. An identifier already ended is TEAM_EXTERNAL_ID_ENDED. " +
                TeamAliasEndpoints.VersionNote)
            .RequireAction(RegistryActions.TeamExternalIdEnd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<EndTeamExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamExternalIdNotFound,
                RegistryErrors.TeamExternalIdEnded, RegistryErrors.TeamExternalIdValidityInvalid);
    }
}
