using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;

internal static class CreateTeamEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapPost("", HandleAsync)
        .WithName("CreateTeam")
        .WithSummary("Add a team")
        .WithDescription(
            "Adds an ACTIVE team to the tenant named by `X-Tenant-Id`. `variant` must be a sport variant the tenant has " +
            "enabled (else TEAM_SPORT_NOT_ENABLED); it never changes. `name`, `genderCategory` and `ageGroup` are required; " +
            "`level` defaults to FIRST. `nameSince` is the day the name began (not after today, the tenant's date; today when " +
            "left out). `organisationId` is an organisation of the tenant that isn't dissolved or archived (else " +
            "TEAM_ORGANISATION_UNAVAILABLE); `homeVenueId` a venue of the tenant that isn't closed or archived (else " +
            "TEAM_HOME_VENUE_UNAVAILABLE). `code` is three upper-case letters; colours are #RRGGBB, and a secondary needs a " +
            "primary, a tertiary a secondary (else TEAM_COLOURS_INVALID). Without `slug`, one is proposed from the name and " +
            "category, with -2, -3… when taken; a slug sent that another team has is TEAM_SLUG_TAKEN. Teams in ACTIVE, " +
            "INACTIVE or SUSPENDED count toward the plan's MAX_TEAMS: at the limit, ENTITLEMENT_QUOTA_EXCEEDED. The duplicate " +
            "check (POST /teams/duplicate-check) runs again: teams that look like this one answer " +
            "TEAM_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request says `duplicatesReviewed: true`, with an " +
            "optional `duplicateReason`; the audit entry names the teams shown. The reason is kept permanently in the audit " +
            "trail: don't include personal data. A name without a letter or a digit is TEAM_NAME_INVALID. Answers the team " +
            "as GET /teams/{teamId} does.")
        .RequireAction(RegistryActions.TeamCreate)
        .ValidateRequest<CreateTeamRequest>(CommonErrors.InvalidRequest)
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.TeamSportNotEnabled, RegistryErrors.TeamNameInvalid, RegistryErrors.TeamColoursInvalid,
            RegistryErrors.TeamOrganisationUnavailable, RegistryErrors.TeamHomeVenueUnavailable, RegistryErrors.TeamSlugTaken,
            RegistryErrors.TeamDuplicateReviewRequired)
        .ProducesProblem<TeamDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Created<TeamResponse>, ProblemHttpResult>> HandleAsync(
        CreateTeamRequest request,
        CreateTeamHandler handler,
        GetTeamHandler read,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var (teamId, refusal) = await handler.HandleAsync(request, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var team = await read.HandleAsync(teamId!.Value, CancellationToken.None);
        team.Value.SetETag(response);
        return TypedResults.Created(TeamsEndpoints.PathOf(team.Value.Id), team.Value);
    }
}
