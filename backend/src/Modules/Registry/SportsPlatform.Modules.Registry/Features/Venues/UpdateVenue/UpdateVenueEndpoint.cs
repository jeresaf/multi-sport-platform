using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

namespace SportsPlatform.Modules.Registry.Features.Venues.UpdateVenue;

internal static class UpdateVenueEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapPut("/{venueId:guid}", HandleAsync)
        .WithName("UpdateVenue")
        .WithSummary("Change a venue")
        .WithDescription(
            "Replaces the fields of a venue of the tenant named by `X-Tenant-Id`, whatever its status: a field left out " +
            "is cleared, `country` left out is the tenant's, and `slug` left out keeps the venue's (a slug another venue " +
            "has is VENUE_SLUG_TAKEN). The status changes through POST /venues/{venueId}/status. When the name (as the " +
            "duplicate check compares it: case, accents, punctuation and word order aside), the city or the coordinates " +
            "change, the duplicate check runs again, the venue itself left out: venues that look like the changed one " +
            "answer VENUE_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request says `duplicatesReviewed: true`, " +
            "with an optional `duplicateReason`; the audit entry names the venues shown. The reason is kept permanently " +
            "in the audit trail: don't include personal data. Send `version` or `If-Match` from the last read to get " +
            "409 VENUE_CONCURRENCY_CONFLICT if the venue changed since. Answers the venue as GET /venues/{venueId} does.")
        .RequireAction(RegistryActions.VenueUpdate)
        .ValidateRequest<UpdateVenueRequest>(CommonErrors.InvalidRequest)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict, RegistryErrors.VenueNameInvalid,
            RegistryErrors.VenueSportUnknown, RegistryErrors.VenueSlugTaken, RegistryErrors.VenueDuplicateReviewRequired)
        .ProducesProblem<VenueDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Ok<VenueResponse>, ProblemHttpResult>> HandleAsync(
        Guid venueId,
        UpdateVenueRequest request,
        UpdateVenueHandler handler,
        GetVenueHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await handler.HandleAsync(venueId, request, ifMatch, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var venue = await read.HandleAsync(venueId, CancellationToken.None);
        venue.Value.SetETag(response);
        return TypedResults.Ok(venue.Value);
    }
}
