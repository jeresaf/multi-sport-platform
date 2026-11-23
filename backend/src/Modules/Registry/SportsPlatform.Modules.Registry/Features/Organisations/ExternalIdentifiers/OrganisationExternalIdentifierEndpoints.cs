using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.Aliases;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ExternalIdentifiers;

/// <summary>
/// <c>POST /organisations/{organisationId}/external-identifiers</c> and
/// <c>POST /organisations/{organisationId}/external-identifiers/{identifierId}/end</c> (S07 D10).
/// </summary>
internal static class OrganisationExternalIdentifierEndpoints
{
    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations
            .MapPost("/{organisationId:guid}/external-identifiers", (
                    Guid organisationId,
                    AddOrganisationExternalIdentifierRequest request,
                    AddOrganisationExternalIdentifierHandler handler,
                    GetOrganisationHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(organisationId, request, ifMatch, cancellationToken)))
            .WithName("AddOrganisationExternalIdentifier")
            .WithSummary("Add an external identifier to an organisation")
            .WithDescription(
                "Adds an identifier another authority gave the organisation: `authority` is a code the tenant chooses (e.g. " +
                "`KMSA_CLUB_NO`), `value` is trimmed. A value with this authority that any organisation has or had, ended ones " +
                "and this organisation's included, is ORGANISATION_EXTERNAL_ID_EXISTS: a value is never reused (a person's identifier " +
                "with the same authority and value doesn't count). The directory finds the organisation by the exact value. A " +
                $"dissolved or archived organisation takes no new identifier (ORGANISATION_DISSOLVED, ORGANISATION_ARCHIVED). {OrganisationAliasEndpoints.VersionNote}")
            .RequireAction(RegistryActions.OrganisationExternalIdAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddOrganisationExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict, RegistryErrors.OrganisationExternalIdExists,
                RegistryErrors.OrganisationArchived, RegistryErrors.OrganisationDissolved);

        organisations
            .MapPost("/{organisationId:guid}/external-identifiers/{identifierId:guid}/end", (
                    Guid organisationId,
                    Guid identifierId,
                    EndOrganisationExternalIdentifierRequest? request,
                    EndOrganisationExternalIdentifierHandler handler,
                    GetOrganisationHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(organisationId, identifierId, request ?? new(), ifMatch, cancellationToken)))
            .WithName("EndOrganisationExternalIdentifier")
            .WithSummary("End an organisation's external identifier")
            .WithDescription(
                "Ends an identifier, whatever the organisation's status: it is kept with its `validTo`, and its value is never " +
                "given to another organisation. The body is optional: `validTo` defaults to today (the tenant's date) and can't be " +
                "after it or before `validFrom` (ORGANISATION_EXTERNAL_ID_VALIDITY_INVALID); `reason` (1–500 characters) is kept " +
                "permanently in the audit trail: don't include personal data. An identifier already ended is " +
                $"ORGANISATION_EXTERNAL_ID_ENDED. {OrganisationAliasEndpoints.VersionNote}")
            .RequireAction(RegistryActions.OrganisationExternalIdEnd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<EndOrganisationExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict, RegistryErrors.OrganisationExternalIdNotFound,
                RegistryErrors.OrganisationExternalIdEnded, RegistryErrors.OrganisationExternalIdValidityInvalid);
    }
}
