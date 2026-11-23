using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

namespace SportsPlatform.Modules.Registry.Features.Organisations.Aliases;

/// <summary><c>POST /organisations/{organisationId}/aliases</c> and <c>POST /organisations/{organisationId}/aliases/{aliasId}/remove</c> (S07 D10).</summary>
internal static class OrganisationAliasEndpoints
{
    public const string VersionNote =
        "Send `version` or `If-Match` from the last read to get 409 ORGANISATION_CONCURRENCY_CONFLICT if the organisation changed since; " +
        "without them, a change to the same organisation saved at the same moment can still answer it. " +
        "Answers the organisation as GET /organisations/{organisationId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations
            .MapPost("/{organisationId:guid}/aliases", (
                    Guid organisationId,
                    AddOrganisationAliasRequest request,
                    AddOrganisationAliasHandler handler,
                    GetOrganisationHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(organisationId, request, ifMatch, cancellationToken)))
            .WithName("AddOrganisationAlias")
            .WithSummary("Add an alias to an organisation")
            .WithDescription(
                "Adds another name the organisation is known by (`FORMER_NAME`, `NICKNAME` or `SPELLING`): the directory search " +
                "finds the organisation under it too, and the duplicate warning of later creates and updates compares it with " +
                "their names. Adding one runs no duplicate warning. A name the organisation is already known by (the same name " +
                "key as its display, legal or short name or a current alias: word order, case, accents and punctuation aside) " +
                "is ORGANISATION_ALIAS_EXISTS; a name without a letter or a digit ORGANISATION_NAME_INVALID. A dissolved or " +
                $"archived organisation takes no new alias (ORGANISATION_DISSOLVED, ORGANISATION_ARCHIVED). {VersionNote}")
            .RequireAction(RegistryActions.OrganisationAliasAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddOrganisationAliasRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict, RegistryErrors.OrganisationNameInvalid,
                RegistryErrors.OrganisationAliasExists, RegistryErrors.OrganisationArchived, RegistryErrors.OrganisationDissolved);

        organisations
            .MapPost("/{organisationId:guid}/aliases/{aliasId:guid}/remove", (
                    Guid organisationId,
                    Guid aliasId,
                    RemoveOrganisationAliasRequest request,
                    RemoveOrganisationAliasHandler handler,
                    GetOrganisationHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                OrganisationChanges.AnswerAsync(organisationId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(organisationId, aliasId, request, ifMatch, cancellationToken)))
            .WithName("RemoveOrganisationAlias")
            .WithSummary("Remove an organisation's alias")
            .WithDescription(
                "Removes an alias, whatever the organisation's status, with a reason (body `{ \"reason\": … }`, required): the alias " +
                "is kept with who removed it, when and why, and is no longer shown or searched. The reason is kept " +
                "permanently on the alias and in the audit trail: don't include personal data. An alias already removed is " +
                $"ORGANISATION_ALIAS_REMOVED. {VersionNote}")
            .RequireAction(RegistryActions.OrganisationAliasRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<RemoveOrganisationAliasRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(
                RegistryErrors.OrganisationNotFound, RegistryErrors.OrganisationConcurrencyConflict, RegistryErrors.OrganisationAliasNotFound,
                RegistryErrors.OrganisationAliasRemoved);
    }
}
