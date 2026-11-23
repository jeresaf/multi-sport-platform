using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.SetOrganisationParent;

internal static class SetOrganisationParentEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapPost("/{organisationId:guid}/parent", HandleAsync)
        .WithName("SetOrganisationParent")
        .WithSummary("Set or clear an organisation's parent")
        .WithDescription(
            "Sets the organisation this one belongs to (body `{ \"parentId\": …, \"reason\": … }`; `parentId: null` clears " +
            "it; the reason is required). The current parent link ends today (the tenant's date) and the new one starts " +
            "today; every link is kept with its dates (GET /organisations/{organisationId}/parent-links). The parent must " +
            "be another organisation of the tenant that isn't dissolved or archived (ORGANISATION_PARENT_UNAVAILABLE), and " +
            "not the organisation itself or any organisation below it (ORGANISATION_PARENT_CYCLE). A dissolved or archived " +
            "organisation takes no new parent (ORGANISATION_DISSOLVED, ORGANISATION_ARCHIVED) but can have its parent " +
            "cleared. Setting the parent it already has changes nothing. Statuses don't follow the parent. The reason is " +
            "kept permanently on the link and in the audit trail: don't include personal data. Send `version` or " +
            "`If-Match` from the last read to get 409 ORGANISATION_CONCURRENCY_CONFLICT if the organisation changed since; " +
            "a change saved to the new parent at the same moment answers it too. Answers the organisation as " +
            "GET /organisations/{organisationId} does.")
        .RequireAction(RegistryActions.OrganisationParentSet)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<SetOrganisationParentRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            CommonErrors.ReasonRequired, RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict,
            RegistryErrors.OrganisationArchived, RegistryErrors.OrganisationDissolved,
            RegistryErrors.OrganisationParentUnavailable, RegistryErrors.OrganisationParentCycle);

    private static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        SetOrganisationParentRequest request,
        SetOrganisationParentHandler handler,
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
            return refusal.ToProblem();
        }

        // Saved and committed: the read is not cancelled with the request.
        var organisation = await read.HandleAsync(organisationId, CancellationToken.None);
        organisation.Value.SetETag(response);
        return TypedResults.Ok(organisation.Value);
    }
}
