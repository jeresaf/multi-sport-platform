using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ChangeOrganisationStatus;

internal static class ChangeOrganisationStatusEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapPost("/{organisationId:guid}/status", HandleAsync)
        .WithName("ChangeOrganisationStatus")
        .WithSummary("Change an organisation's status")
        .WithDescription(
            "Moves the organisation, with a reason (body `{ \"status\": …, \"reason\": … }`, both required): ACTIVE, " +
            "INACTIVE and SUSPENDED move between each other and to DISSOLVED or ARCHIVED; DISSOLVED → ARCHIVED only; " +
            "ARCHIVED → the status it had before archiving (unarchive), and no other, so a dissolved organisation stays " +
            "dissolved. Any other move is ORGANISATION_STATUS_INVALID. The organisation's teams keep their statuses. " +
            "Nothing is deleted: each move is kept in the organisation's history " +
            "(GET /organisations/{organisationId}/status-changes). The reason is kept permanently in that history and the " +
            "audit trail: don't include personal data. Send `version` or `If-Match` from the last read to get 409 " +
            "ORGANISATION_CONCURRENCY_CONFLICT if the organisation changed since. Answers the organisation as " +
            "GET /organisations/{organisationId} does.")
        .RequireAction(RegistryActions.OrganisationStatusChange)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<ChangeOrganisationStatusRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            CommonErrors.ReasonRequired, RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict,
            RegistryErrors.OrganisationStatusInvalid);

    private static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        ChangeOrganisationStatusRequest request,
        ChangeOrganisationStatusHandler handler,
        GetOrganisationHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(organisationId, request, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var organisation = await read.HandleAsync(organisationId, CancellationToken.None);
        organisation.Value.SetETag(response);
        return TypedResults.Ok(organisation.Value);
    }
}
