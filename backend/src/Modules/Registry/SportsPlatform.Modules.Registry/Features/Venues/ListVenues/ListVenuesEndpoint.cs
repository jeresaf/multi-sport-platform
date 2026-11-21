using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

internal static class ListVenuesEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapGet("", HandleAsync)
        .WithName("ListVenues")
        .WithSummary("List the tenant's venues")
        .WithDescription(
            "The venues of the tenant named by `X-Tenant-Id`, sorted by name (ignoring case, accents and punctuation) " +
            "and ID. `q` matches the name or a current alias, or one of their words, by prefix and by similarity, and an " +
            "external identifier's value exactly (ended ones included). `variant` keeps " +
            "venues that support that sport variant (a code); `city` venues in that city (ignoring case, accents and " +
            "punctuation); `status` filters by status (without it, archived venues are left out). `limit` is 1–100 (25). " +
            "Available while the tenant is read-only.")
        .RequireAction(RegistryActions.VenueRead)
        .ValidateRequest<ListVenuesRequest>(CommonErrors.InvalidQuery)
        .DocumentsQueryValues("status", ListVenuesRequest.Statuses)
        .DocumentsQueryInteger("limit", 1, ListVenuesRequest.MaxLimit, ListVenuesRequest.DefaultLimit);

    private static async Task<Ok<ListVenuesResponse>> HandleAsync(
        [AsParameters] ListVenuesRequest request,
        ListVenuesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
