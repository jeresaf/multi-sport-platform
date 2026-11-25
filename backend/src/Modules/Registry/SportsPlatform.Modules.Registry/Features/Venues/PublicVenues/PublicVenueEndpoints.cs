using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

namespace SportsPlatform.Modules.Registry.Features.Venues.PublicVenues;

/// <summary>
/// <c>GET /api/public/v1/venues</c> and <c>…/{idOrSlug}</c> (S07 D17): the tenant comes from the request host. No
/// per-record switch (venues aren't personal data); every status but <c>ARCHIVED</c> is public. HTTP caches may keep both
/// for a minute.
/// </summary>
internal static class PublicVenueEndpoints
{
    public const string Tag = "Public venues";

    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    public static void Map(IEndpointRouteBuilder publicApi)
    {
        var venues = publicApi.MapGroup("/venues").WithTags(Tag);

        venues
            .MapGet("", ListAsync)
            .WithName("ListPublicVenues")
            .WithSummary("List venues on the public site")
            .WithDescription(
                "The venues of the tenant whose site this host serves, sorted by name (ignoring case, accents and " +
                "punctuation) and ID. `status` keeps one status (`ACTIVE` when left out; archived venues aren't public). " +
                "`variant` keeps venues that support that sport variant (a code). `q` matches the name or one of its " +
                "words by prefix and by similarity. `limit` is 1–100 (25). Cacheable for a minute.")
            .RequirePublicTenant()
            .ValidateRequest<ListPublicVenuesRequest>(CommonErrors.InvalidQuery)
            .DocumentsQueryValues("status", ListPublicVenuesRequest.Statuses)
            .DocumentsQueryInteger("limit", 1, ListVenuesRequest.MaxLimit, ListVenuesRequest.DefaultLimit);

        venues
            .MapGet("/{idOrSlug}", GetAsync)
            .WithName("GetPublicVenue")
            .WithSummary("Get a venue on the public site")
            .WithDescription(
                "A venue of the tenant whose site this host serves, by ID or slug, in any status but `ARCHIVED` " +
                "(VENUE_NOT_FOUND). Its public fields only: names, type, address, city, region, country, coordinates, " +
                "time zone, capacity, surfaces, supported variants and status. Cacheable for a minute; send the `ETag` " +
                "back as `If-None-Match` to get 304.")
            .RequirePublicTenant()
            .ProducesErrors(RegistryErrors.VenueNotFound);
    }

    private static async Task<CacheableJson<ListPublicVenuesResponse>> ListAsync(
        [AsParameters] ListPublicVenuesRequest request,
        ListPublicVenuesHandler handler,
        CancellationToken cancellationToken) =>
        CacheableJson.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken), MaxAge);

    private static async Task<Results<CacheableJson<PublicVenueResponse>, ProblemHttpResult>> GetAsync(
        string idOrSlug, GetPublicVenueHandler handler, CancellationToken cancellationToken)
    {
        var venue = await handler.HandleAsync(idOrSlug, cancellationToken);
        if (venue.IsFailure)
        {
            return venue.Error.ToProblem();
        }

        return CacheableJson.Ok(venue.Value, MaxAge);
    }
}
