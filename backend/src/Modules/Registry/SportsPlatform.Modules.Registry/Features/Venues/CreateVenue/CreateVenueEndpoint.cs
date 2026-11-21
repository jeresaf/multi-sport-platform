using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

namespace SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;

internal static class CreateVenueEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapPost("", HandleAsync)
        .WithName("CreateVenue")
        .WithSummary("Add a venue")
        .WithDescription(
            "Adds an ACTIVE venue to the tenant named by `X-Tenant-Id`. `timeZone` is required (an active IANA zone); " +
            "`country` defaults to the tenant's; `latitude` and `longitude` come together or not at all; " +
            "`supportedVariants` are published sport variants, enabled for the tenant or not (else VENUE_SPORT_UNKNOWN). " +
            "Without `slug`, one is proposed from the name, with -2, -3… when taken; a slug sent that another venue has " +
            "is VENUE_SLUG_TAKEN. The duplicate check (POST /venues/duplicate-check) runs again: venues that look like " +
            "this one answer VENUE_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request says " +
            "`duplicatesReviewed: true`, with an optional `duplicateReason`; the audit entry names the venues shown. " +
            "The reason is kept permanently in the audit trail: don't include personal data. A name without a letter or " +
            "a digit is VENUE_NAME_INVALID. Answers the venue as GET /venues/{venueId} does.")
        .RequireAction(RegistryActions.VenueCreate)
        .ValidateRequest<CreateVenueRequest>(CommonErrors.InvalidRequest)
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.VenueNameInvalid, RegistryErrors.VenueSportUnknown, RegistryErrors.VenueSlugTaken,
            RegistryErrors.VenueDuplicateReviewRequired);

    private static async Task<Results<Created<VenueResponse>, ProblemHttpResult>> HandleAsync(
        CreateVenueRequest request,
        CreateVenueHandler handler,
        GetVenueHandler read,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var (venueId, refusal) = await handler.HandleAsync(request, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var venue = await read.HandleAsync(venueId!.Value, CancellationToken.None);
        venue.Value.SetETag(response);
        return TypedResults.Created(VenuesEndpoints.PathOf(venue.Value.Id), venue.Value);
    }
}
