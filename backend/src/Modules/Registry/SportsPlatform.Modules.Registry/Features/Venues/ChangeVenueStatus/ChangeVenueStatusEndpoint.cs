using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

namespace SportsPlatform.Modules.Registry.Features.Venues.ChangeVenueStatus;

internal static class ChangeVenueStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapPost("/{venueId:guid}/status", HandleAsync)
        .WithName("ChangeVenueStatus")
        .WithSummary("Change a venue's status")
        .WithDescription(
            "Moves the venue, with a reason (body `{ \"status\": …, \"reason\": … }`, both required): ACTIVE ⇄ " +
            "TEMPORARILY_UNAVAILABLE; ACTIVE or TEMPORARILY_UNAVAILABLE → CLOSED; CLOSED → ACTIVE (reopen); any status but " +
            "ARCHIVED → ARCHIVED; ARCHIVED → the status it had before archiving (unarchive), and no other. Any other move " +
            "is VENUE_STATUS_INVALID. Nothing is deleted: each move is kept in the venue's history " +
            "(GET /venues/{venueId}/status-changes). The reason is kept permanently in that history and the audit " +
            "trail: don't include personal data. Send `version` or `If-Match` from the last read to get 409 " +
            "VENUE_CONCURRENCY_CONFLICT if the venue changed since. Answers the venue as GET /venues/{venueId} does.")
        .RequireAction(RegistryActions.VenueStatusChange)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<ChangeVenueStatusRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            CommonErrors.ReasonRequired, RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict,
            RegistryErrors.VenueStatusInvalid);

    private static async Task<Results<Ok<VenueResponse>, ProblemHttpResult>> HandleAsync(
        Guid venueId,
        ChangeVenueStatusRequest request,
        ChangeVenueStatusHandler handler,
        GetVenueHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(venueId, request, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var venue = await read.HandleAsync(venueId, CancellationToken.None);
        venue.Value.SetETag(response);
        return TypedResults.Ok(venue.Value);
    }
}
