using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

namespace SportsPlatform.Modules.Registry.Features.Teams.Images;

/// <summary>
/// <c>PUT</c> and <c>DELETE /teams/{teamId}/logo</c> and <c>/teams/{teamId}/badge</c> (S07 D15). Like every state-changing
/// admin request the form needs <c>X-CSRF: 1</c>, not ASP.NET Core's antiforgery.
/// </summary>
internal static class TeamImageEndpoints
{
    private const string _versionNote =
        "Send `If-Match` from the last read to get 409 TEAM_CONCURRENCY_CONFLICT if the team changed since. Answers the team " +
        "as GET /teams/{teamId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder teams)
    {
        MapImage(teams, TeamImage.Logo, "logo", RegistryActions.TeamLogoSet, RegistryActions.TeamLogoRemove, RegistryErrors.TeamLogoNotFound);
        MapImage(teams, TeamImage.Badge, "badge", RegistryActions.TeamBadgeSet, RegistryActions.TeamBadgeRemove, RegistryErrors.TeamBadgeNotFound);
    }

    private static void MapImage(
        IEndpointRouteBuilder teams, TeamImage image, string name, ActionDefinition set, ActionDefinition remove, Error notFound)
    {
        var title = image == TeamImage.Logo ? "Logo" : "Badge";

        teams
            .MapPut($"/{{teamId:guid}}/{name}", async (
                Guid teamId,
                IFormFile? file,
                SetTeamImageHandler handler,
                GetTeamHandler read,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                if (RegistryMedia.RefuseUnlessOneFile(file, httpRequest) is { } malformed)
                {
                    return (Results<Ok<TeamResponse>, ProblemHttpResult>)malformed.ToProblem();
                }

                return await TeamChanges.AnswerAsync(teamId, read, httpRequest, response, async ifMatch =>
                {
                    await using var content = file!.OpenReadStream();
                    return await handler.HandleAsync(teamId, image, content, ifMatch, cancellationToken);
                });
            })
            .WithName($"SetTeam{title}")
            .WithSummary($"Set a team's {name}")
            .WithDescription(
                RegistryMedia.ImageUploadNote(name) +
                $"The team's other image doesn't change. A disbanded or archived team takes no new {name} (TEAM_DISBANDED, " +
                "TEAM_ARCHIVED). " + _versionNote)
            .RequireAction(set)
            .DisableAntiforgery()
            .WithMetadata(new StoresMediaMetadata())
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(
                RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, RegistryErrors.TeamArchived,
                RegistryErrors.TeamDisbanded, CommonErrors.InvalidRequest);

        teams
            .MapDelete($"/{{teamId:guid}}/{name}", (
                    Guid teamId,
                    RemoveTeamImageHandler handler,
                    GetTeamHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                TeamChanges.AnswerAsync(teamId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(teamId, image, ifMatch, cancellationToken)))
            .WithName($"RemoveTeam{title}")
            .WithSummary($"Remove a team's {name}")
            .WithDescription(
                $"Removes the team's {name}, whatever its status; a team without one is {notFound.Code}. " + _versionNote)
            .RequireAction(remove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ProducesErrors(RegistryErrors.TeamNotFound, RegistryErrors.TeamConcurrencyConflict, notFound);
    }
}
