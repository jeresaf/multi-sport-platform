using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.UpdateTeam;

internal static class UpdateTeamEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapPut("/{teamId:guid}", HandleAsync)
        .WithName("UpdateTeam")
        .WithSummary("Change a team")
        .WithDescription(
            "Replaces the fields of a team of the tenant named by `X-Tenant-Id`, whatever its status and whether or not its " +
            "sport is still enabled: a field left out is cleared, `level` left out is FIRST, and `slug` left out keeps the " +
            "team's (a slug another team has is TEAM_SLUG_TAKEN). The sport never changes and can't be sent. `name` may be " +
            "sent, but only as the current name: a new name is a rename (POST /teams/{teamId}/rename; TEAM_RENAME_REQUIRED), " +
            "so the old one is kept with its dates. The status changes through POST /teams/{teamId}/status. A new `organisationId` must be an " +
            "organisation of the tenant that isn't dissolved or archived (TEAM_ORGANISATION_UNAVAILABLE), a new " +
            "`homeVenueId` a venue that isn't closed or archived (TEAM_HOME_VENUE_UNAVAILABLE); the current ones are kept " +
            "whatever their status. When the short name (as the duplicate check compares it: case, accents, punctuation " +
            "and word order aside), the category or the organisation changes, the duplicate check runs again, the team " +
            "itself left out: teams that look like the changed one answer TEAM_DUPLICATE_REVIEW_REQUIRED with " +
            "`candidates` until the request says `duplicatesReviewed: true`, with an optional `duplicateReason`; the audit " +
            "entry names the teams shown. The reason is kept permanently in the audit trail: don't include personal data. " +
            "Send `version` or `If-Match` from the last read to get 409 TEAM_CONCURRENCY_CONFLICT if the team changed " +
            "since. Answers the team as GET /teams/{teamId} does.")
        .RequireAction(RegistryActions.TeamUpdate)
        .ValidateRequest<UpdateTeamRequest>(CommonErrors.InvalidRequest)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamRenameRequired,
            RegistryErrors.TeamColoursInvalid, RegistryErrors.TeamOrganisationUnavailable,
            RegistryErrors.TeamHomeVenueUnavailable, RegistryErrors.TeamSlugTaken, RegistryErrors.TeamDuplicateReviewRequired)
        .ProducesProblem<TeamDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Ok<TeamResponse>, ProblemHttpResult>> HandleAsync(
        Guid teamId,
        UpdateTeamRequest request,
        UpdateTeamHandler handler,
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
