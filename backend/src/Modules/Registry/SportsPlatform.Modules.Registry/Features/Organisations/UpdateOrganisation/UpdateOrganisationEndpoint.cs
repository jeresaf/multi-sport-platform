using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.UpdateOrganisation;

internal static class UpdateOrganisationEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapPut("/{organisationId:guid}", HandleAsync)
        .WithName("UpdateOrganisation")
        .WithSummary("Change an organisation")
        .WithDescription(
            "Replaces the fields of an organisation of the tenant named by `X-Tenant-Id`, whatever its status: a field " +
            "left out is cleared, `country` left out is the tenant's, and `slug` left out keeps the organisation's (a slug " +
            "another organisation has is ORGANISATION_SLUG_TAKEN). The status changes through POST " +
            "/organisations/{organisationId}/status. A new `primaryVenueId` must be a venue of the tenant that isn't " +
            "closed or archived (ORGANISATION_PRIMARY_VENUE_UNAVAILABLE); the current one is kept whatever its status. " +
            "When a name (as the duplicate check compares it: case, accents, punctuation and word order aside), the type " +
            "or the city changes, the duplicate check runs again, the organisation itself left out: organisations that " +
            "look like the changed one answer ORGANISATION_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request " +
            "says `duplicatesReviewed: true`, with an optional `duplicateReason`; the audit entry names the organisations " +
            "shown. The reason is kept permanently in the audit trail: don't include personal data. Send `version` or " +
            "`If-Match` from the last read to get 409 ORGANISATION_CONCURRENCY_CONFLICT if the organisation changed since. " +
            "Answers the organisation as GET /organisations/{organisationId} does.")
        .RequireAction(RegistryActions.OrganisationUpdate)
        .ValidateRequest<UpdateOrganisationRequest>(CommonErrors.InvalidRequest)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict,
            RegistryErrors.OrganisationNameInvalid, RegistryErrors.OrganisationPrimaryVenueUnavailable,
            RegistryErrors.OrganisationSlugTaken, RegistryErrors.OrganisationDuplicateReviewRequired)
        .ProducesProblem<OrganisationDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        UpdateOrganisationRequest request,
        UpdateOrganisationHandler handler,
        GetOrganisationHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await handler.HandleAsync(organisationId, request, ifMatch, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var organisation = await read.HandleAsync(organisationId, CancellationToken.None);
        organisation.Value.SetETag(response);
        return TypedResults.Ok(organisation.Value);
    }
}
