using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

internal static class ListTeamsEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapGet("", HandleAsync)
        .WithName("ListTeams")
        .WithSummary("List the tenant's teams")
        .WithDescription(
            "The teams of the tenant named by `X-Tenant-Id`, sorted by name (ignoring case, accents and punctuation), then " +
            "category (age group `SENIOR`, `U6` … `U23`, `VETERANS`; then gender `MEN`, `WOMEN`, `MIXED`; then level " +
            "`FIRST`, `RESERVE`, `DEVELOPMENT`, `ACADEMY`), then ID. `q` matches the name or one of its words by prefix " +
            "and by similarity, the short name by prefix (word order and punctuation aside), a current nickname, spelling or " +
            "former name by prefix and by similarity, and an external identifier's value exactly (ended ones included). `variant`, " +
            "`organisationId`, `homeVenueId`, `genderCategory`, `ageGroup` and `level` keep the teams that have them (an " +
            "unknown variant, organisation or venue keeps none); `status` filters by status (without it, archived teams are left out). Each item " +
            "carries its organisation (id, display name, slug), whatever its status now. `limit` is 1–100 (25). " +
            "Available while the tenant is read-only.")
        .RequireAction(RegistryActions.TeamRead)
        .ValidateRequest<ListTeamsRequest>(CommonErrors.InvalidQuery)
        .DocumentsQueryUuid("organisationId")
        .DocumentsQueryUuid("homeVenueId")
        .DocumentsQueryValues("genderCategory", ListTeamsRequest.GenderCategories)
        .DocumentsQueryValues("ageGroup", ListTeamsRequest.AgeGroups)
        .DocumentsQueryValues("level", ListTeamsRequest.Levels)
        .DocumentsQueryValues("status", ListTeamsRequest.Statuses)
        .DocumentsQueryInteger("limit", 1, ListTeamsRequest.MaxLimit, ListTeamsRequest.DefaultLimit);

    private static async Task<Ok<ListTeamsResponse>> HandleAsync(
        [AsParameters] ListTeamsRequest request,
        ListTeamsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
