using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.PublicOrganisations;

/// <summary>
/// <c>GET /api/public/v1/organisations</c> and <c>…/{idOrSlug}</c> (S07 D17): the tenant comes from the request host. No
/// per-record switch (organisations aren't personal data); every status but <c>ARCHIVED</c> is public. HTTP caches may
/// keep both for a minute.
/// </summary>
internal static class PublicOrganisationEndpoints
{
    public const string Tag = "Public organisations";

    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    public static void Map(IEndpointRouteBuilder publicApi)
    {
        var organisations = publicApi.MapGroup("/organisations").WithTags(Tag);

        organisations
            .MapGet("", ListAsync)
            .WithName("ListPublicOrganisations")
            .WithSummary("List organisations on the public site")
            .WithDescription(
                "The organisations of the tenant whose site this host serves, sorted by display name (ignoring case, " +
                "accents and punctuation) and ID. `status` keeps one status (`ACTIVE` when left out; archived " +
                "organisations aren't public). `type` keeps that type. `q` matches the display name or one of its words " +
                "by prefix and by similarity, and the short name by prefix (word order and punctuation aside). `limit` " +
                "is 1–100 (25). `logo` holds the public asset route's paths (null without one). Cacheable for a minute.")
            .RequirePublicTenant()
            .ValidateRequest<ListPublicOrganisationsRequest>(CommonErrors.InvalidQuery)
            .DocumentsQueryValues("type", ListOrganisationsRequest.Types)
            .DocumentsQueryValues("status", ListPublicOrganisationsRequest.Statuses)
            .DocumentsQueryInteger("limit", 1, ListOrganisationsRequest.MaxLimit, ListOrganisationsRequest.DefaultLimit);

        organisations
            .MapGet("/{idOrSlug}", GetAsync)
            .WithName("GetPublicOrganisation")
            .WithSummary("Get an organisation on the public site")
            .WithDescription(
                "An organisation of the tenant whose site this host serves, by ID or slug, in any status but `ARCHIVED` " +
                "(ORGANISATION_NOT_FOUND). Its public fields only: type, names, country, city, founded year, website, " +
                "description, `primaryVenue` (id, name, slug), `parent` (id, display name, slug, status), `children` and " +
                "`teams` (as the admin read's), status and `logo`. An archived primary venue or parent is null; archived " +
                "children and teams are left out. Cacheable for a minute; send the `ETag` back as `If-None-Match` to " +
                "get 304.")
            .RequirePublicTenant()
            .ProducesErrors(RegistryErrors.OrganisationNotFound);
    }

    private static async Task<CacheableJson<ListPublicOrganisationsResponse>> ListAsync(
        [AsParameters] ListPublicOrganisationsRequest request,
        ListPublicOrganisationsHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken), MaxAge);

    private static async Task<Results<CacheableJson<PublicOrganisationResponse>, ProblemHttpResult>> GetAsync(
        string idOrSlug, GetPublicOrganisationHandler handler, CancellationToken cancellationToken)
    {
        var organisation = await handler.HandleAsync(idOrSlug, cancellationToken);
        if (organisation.IsFailure)
        {
            return organisation.Error.ToProblem();
        }

        return CacheableJson.Ok(organisation.Value, MaxAge);
    }
}
