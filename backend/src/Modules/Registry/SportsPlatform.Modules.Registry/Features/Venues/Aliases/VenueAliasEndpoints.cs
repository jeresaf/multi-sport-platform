using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

namespace SportsPlatform.Modules.Registry.Features.Venues.Aliases;

/// <summary><c>POST /venues/{venueId}/aliases</c> and <c>POST /venues/{venueId}/aliases/{aliasId}/remove</c> (S07 D10).</summary>
internal static class VenueAliasEndpoints
{
    public const string VersionNote =
        "Send `version` or `If-Match` from the last read to get 409 VENUE_CONCURRENCY_CONFLICT if the venue changed since; " +
        "without them, a change to the same venue saved at the same moment can still answer it. " +
        "Answers the venue as GET /venues/{venueId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder venues)
    {
        venues
            .MapPost("/{venueId:guid}/aliases", (
                    Guid venueId,
                    AddVenueAliasRequest request,
                    AddVenueAliasHandler handler,
                    GetVenueHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                VenueChanges.AnswerAsync(venueId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(venueId, request, ifMatch, cancellationToken)))
            .WithName("AddVenueAlias")
            .WithSummary("Add an alias to a venue")
            .WithDescription(
                "Adds another name the venue is known by (`FORMER_NAME`, `NICKNAME` or `SPELLING`): the directory search " +
                "finds the venue under it too; the duplicate warning compares the venue's own name only. A name the venue " +
                "is already known by (the same name key as its own name or a current alias: word order, case, accents and " +
                "punctuation aside) is VENUE_ALIAS_EXISTS; a name without a letter or a digit VENUE_NAME_INVALID. An " +
                $"archived or closed venue takes no new alias (VENUE_ARCHIVED, VENUE_CLOSED). {VersionNote}")
            .RequireAction(RegistryActions.VenueAliasAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddVenueAliasRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict, RegistryErrors.VenueNameInvalid,
                RegistryErrors.VenueAliasExists, RegistryErrors.VenueArchived, RegistryErrors.VenueClosed);

        venues
            .MapPost("/{venueId:guid}/aliases/{aliasId:guid}/remove", (
                    Guid venueId,
                    Guid aliasId,
                    RemoveVenueAliasRequest request,
                    RemoveVenueAliasHandler handler,
                    GetVenueHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                VenueChanges.AnswerAsync(venueId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(venueId, aliasId, request, ifMatch, cancellationToken)))
            .WithName("RemoveVenueAlias")
            .WithSummary("Remove a venue's alias")
            .WithDescription(
                "Removes an alias, whatever the venue's status, with a reason (body `{ \"reason\": … }`, required): the alias " +
                "is kept with who removed it, when and why, and is no longer shown or searched. The reason is kept " +
                "permanently on the alias and in the audit trail: don't include personal data. An alias already removed is " +
                $"VENUE_ALIAS_REMOVED. {VersionNote}")
            .RequireAction(RegistryActions.VenueAliasRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<RemoveVenueAliasRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(
                RegistryErrors.VenueNotFound, RegistryErrors.VenueConcurrencyConflict, RegistryErrors.VenueAliasNotFound,
                RegistryErrors.VenueAliasRemoved);
    }
}
