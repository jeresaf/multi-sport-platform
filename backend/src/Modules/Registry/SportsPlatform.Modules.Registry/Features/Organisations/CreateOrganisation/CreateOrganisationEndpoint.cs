using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;

internal static class CreateOrganisationEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapPost("", HandleAsync)
        .WithName("CreateOrganisation")
        .WithSummary("Add an organisation")
        .WithDescription(
            "Adds an ACTIVE organisation (a club, school, federation…) to the tenant named by `X-Tenant-Id`. `type` and " +
            "`displayName` are required; `country` defaults to the tenant's; `foundedYear` is 1800 to this year (the " +
            "tenant's date); `website` is an absolute https:// or http:// address; `email` and `phone` are the body's own " +
            "contacts, not a person's. `primaryVenueId` is a venue of the tenant that isn't closed or archived (else " +
            "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE). Without `slug`, one is proposed from the display name, with -2, -3… " +
            "when taken; a slug sent that another organisation has is ORGANISATION_SLUG_TAKEN. The duplicate check (POST " +
            "/organisations/duplicate-check) runs again: organisations that look like this one answer " +
            "ORGANISATION_DUPLICATE_REVIEW_REQUIRED with `candidates` until the request says `duplicatesReviewed: true`, " +
            "with an optional `duplicateReason`; the audit entry names the organisations shown. The reason is kept " +
            "permanently in the audit trail: don't include personal data. A display name without a letter or a digit is " +
            "ORGANISATION_NAME_INVALID. Answers the organisation as GET /organisations/{organisationId} does.")
        .RequireAction(RegistryActions.OrganisationCreate)
        .ValidateRequest<CreateOrganisationRequest>(CommonErrors.InvalidRequest)
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.OrganisationNameInvalid, RegistryErrors.OrganisationPrimaryVenueUnavailable,
            RegistryErrors.OrganisationSlugTaken, RegistryErrors.OrganisationDuplicateReviewRequired)
        .ProducesProblem<OrganisationDuplicateReviewProblem>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Created<OrganisationResponse>, ProblemHttpResult>> HandleAsync(
        CreateOrganisationRequest request,
        CreateOrganisationHandler handler,
        GetOrganisationHandler read,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var (organisationId, refusal) = await handler.HandleAsync(request, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var organisation = await read.HandleAsync(organisationId!.Value, CancellationToken.None);
        organisation.Value.SetETag(response);
        return TypedResults.Created(OrganisationsEndpoints.PathOf(organisation.Value.Id), organisation.Value);
    }
}
