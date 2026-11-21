using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.Aliases;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

namespace SportsPlatform.Modules.Registry.Features.Venues.ExternalIdentifiers;

/// <summary>
/// <c>POST /venues/{venueId}/external-identifiers</c> and
/// <c>POST /venues/{venueId}/external-identifiers/{identifierId}/end</c> (S07 D10).
/// </summary>
internal static class VenueExternalIdentifierEndpoints
{
    public static void Map(IEndpointRouteBuilder venues)
    {
        venues
            .MapPost("/{venueId:guid}/external-identifiers", (
                    Guid venueId,
                    AddVenueExternalIdentifierRequest request,
                    AddVenueExternalIdentifierHandler handler,
                    GetVenueHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                VenueChanges.AnswerAsync(venueId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(venueId, request, ifMatch, cancellationToken)))
            .WithName("AddVenueExternalIdentifier")
            .WithSummary("Add an external identifier to a venue")
            .WithDescription(
                "Adds an identifier another authority gave the venue: `authority` is a code the tenant chooses (e.g. " +
                "`KCCA_FACILITY_NO`), `value` is trimmed. A value with this authority that any venue has or had, ended ones " +
                "and this venue's included, is VENUE_EXTERNAL_ID_EXISTS: a value is never reused (a person's identifier " +
                "with the same authority and value doesn't count). The directory finds the venue by the exact value. An " +
                $"archived or closed venue takes no new identifier (VENUE_ARCHIVED, VENUE_CLOSED). {VenueAliasEndpoints.VersionNote}")
            .RequireAction(RegistryActions.VenueExternalIdAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddVenueExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict, RegistryErrors.VenueExternalIdExists,
                RegistryErrors.VenueArchived, RegistryErrors.VenueClosed);

        venues
            .MapPost("/{venueId:guid}/external-identifiers/{identifierId:guid}/end", (
                    Guid venueId,
                    Guid identifierId,
                    EndVenueExternalIdentifierRequest? request,
                    EndVenueExternalIdentifierHandler handler,
                    GetVenueHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                VenueChanges.AnswerAsync(venueId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(venueId, identifierId, request ?? new(), ifMatch, cancellationToken)))
            .WithName("EndVenueExternalIdentifier")
            .WithSummary("End a venue's external identifier")
            .WithDescription(
                "Ends an identifier, whatever the venue's status: it is kept with its `validTo`, and its value is never " +
                "given to another venue. The body is optional: `validTo` defaults to today (the tenant's date) and can't be " +
                "after it or before `validFrom` (VENUE_EXTERNAL_ID_VALIDITY_INVALID); `reason` (1–500 characters) is kept " +
                "permanently in the audit trail: don't include personal data. An identifier already ended is " +
                $"VENUE_EXTERNAL_ID_ENDED. {VenueAliasEndpoints.VersionNote}")
            .RequireAction(RegistryActions.VenueExternalIdEnd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<EndVenueExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict, RegistryErrors.VenueExternalIdNotFound,
                RegistryErrors.VenueExternalIdEnded, RegistryErrors.VenueExternalIdValidityInvalid);
    }
}
