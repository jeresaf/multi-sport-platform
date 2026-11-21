using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

internal static class GetVenueEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapGet("/{venueId:guid}", HandleAsync)
        .WithName("GetVenue")
        .WithSummary("Get a venue")
        .WithDescription(
            "A venue of the tenant named by `X-Tenant-Id`, whatever its status: address, time zone, coordinates, " +
            "capacity, surfaces, supported sport variants (codes; labels come from the definitions), status and " +
            "`version` (also the `ETag`).")
        .RequireAction(RegistryActions.VenueRead)
        .ReturnsETag()
        .ProducesErrors(RegistryErrors.VenueNotFound);

    private static async Task<Results<Ok<VenueResponse>, ProblemHttpResult>> HandleAsync(
        Guid venueId,
        GetVenueHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(venueId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
