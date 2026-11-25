using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

namespace SportsPlatform.Modules.Registry.Features.Teams.PublicTeams;

/// <summary>
/// <c>GET /api/public/v1/teams</c> and <c>…/{idOrSlug}</c> (S07 D17): the tenant comes from the request host. No
/// per-record switch (teams aren't personal data); every status but <c>ARCHIVED</c> is public. HTTP caches may keep both
/// for a minute.
/// </summary>
internal static class PublicTeamEndpoints
{
    public const string Tag = "Public teams";

    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    public static void Map(IEndpointRouteBuilder publicApi)
    {
        var teams = publicApi.MapGroup("/teams").WithTags(Tag);

        teams
            .MapGet("", ListAsync)
            .WithName("ListPublicTeams")
            .WithSummary("List teams on the public site")
            .WithDescription(
                "The teams of the tenant whose site this host serves, sorted by name (ignoring case, accents and " +
                "punctuation), then category (age group `SENIOR`, `U6` … `U23`, `VETERANS`; then gender `MEN`, `WOMEN`, " +
                "`MIXED`; then level `FIRST`, `RESERVE`, `DEVELOPMENT`, `ACADEMY`), then ID. `status` keeps one status " +
                "(`ACTIVE` when left out; archived teams aren't public). `variant`, `organisationId`, `genderCategory`, " +
                "`ageGroup` and `level` keep the teams that have them (an unknown variant or organisation keeps none). `q` " +
                "matches the name or one of its words by prefix and by similarity, the short name by prefix, and a former " +
                "name by prefix and by similarity (word order and punctuation aside). Each item carries its organisation " +
                "(id, display name, slug; null when independent or archived). `limit` is 1–100 (25). `logo` and `badge` " +
                "hold the public asset route's paths (null without one). Cacheable for a minute.")
            .RequirePublicTenant()
            .ValidateRequest<ListPublicTeamsRequest>(CommonErrors.InvalidQuery)
            .DocumentsQueryUuid("organisationId")
            .DocumentsQueryValues("genderCategory", ListTeamsRequest.GenderCategories)
            .DocumentsQueryValues("ageGroup", ListTeamsRequest.AgeGroups)
            .DocumentsQueryValues("level", ListTeamsRequest.Levels)
            .DocumentsQueryValues("status", ListPublicTeamsRequest.Statuses)
            .DocumentsQueryInteger("limit", 1, ListTeamsRequest.MaxLimit, ListTeamsRequest.DefaultLimit);

        teams
            .MapGet("/{idOrSlug}", GetAsync)
            .WithName("GetPublicTeam")
            .WithSummary("Get a team on the public site")
            .WithDescription(
                "A team of the tenant whose site this host serves, by ID or slug, in any status but `ARCHIVED` " +
                "(TEAM_NOT_FOUND). Its public fields only: variant, category, names and when the current name began, " +
                "slug, code, motto, colours, founded year, `organisation` and `homeVenue` (id, name, slug), status, " +
                "`logo`, `badge` and `formerNames` (with the short name and code that went with each, and its first and " +
                "last day; newest first). An archived organisation or home venue is null. Cacheable for a minute; send " +
                "the `ETag` back as `If-None-Match` to get 304.")
            .RequirePublicTenant()
            .ProducesErrors(RegistryErrors.TeamNotFound);
    }

    private static async Task<CacheableJson<ListPublicTeamsResponse>> ListAsync(
        [AsParameters] ListPublicTeamsRequest request,
        ListPublicTeamsHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken), MaxAge);

    private static async Task<Results<CacheableJson<PublicTeamResponse>, ProblemHttpResult>> GetAsync(
        string idOrSlug, GetPublicTeamHandler handler, CancellationToken cancellationToken)
    {
        var team = await handler.HandleAsync(idOrSlug, cancellationToken);
        if (team.IsFailure)
        {
            return team.Error.ToProblem();
        }

        return CacheableJson.Ok(team.Value, MaxAge);
    }
}
