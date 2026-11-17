using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.ExternalIdentifiers;

/// <summary>
/// <c>POST /people/{personId}/external-identifiers</c> and
/// <c>POST /people/{personId}/external-identifiers/{identifierId}/end</c> (S06 D10).
/// </summary>
internal static class ExternalIdentifierEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since; " +
        "without them, a change to the same person saved at the same moment can still answer it. " +
        "Answers the person as GET /people/{personId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/external-identifiers", (
                    Guid personId,
                    AddExternalIdentifierRequest request,
                    AddExternalIdentifierHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, request, ifMatch, cancellationToken)))
            .WithName("AddPersonExternalIdentifier")
            .WithSummary("Add an external identifier to a person")
            .WithDescription(
                "Adds an identifier another authority gave the person: `authority` is a code the tenant chooses (e.g. " +
                "`KMSA_REG_NO`), `value` is trimmed. A value with this authority that any person has or had, ended ones " +
                "and this person's included, is PERSON_EXTERNAL_ID_EXISTS: a value is never reused. National identity " +
                "documents (`NATIONAL_ID`, `NIN`, `PASSPORT`) are PERSON_EXTERNAL_ID_AUTHORITY_RESERVED. Names and dates " +
                "aren't checked for duplicates. An archived or deceased person takes no new identifier (PERSON_ARCHIVED, " +
                $"PERSON_DECEASED). {_versionNote}")
            .RequireAction(RegistryActions.PersonExternalIdAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ExternalIdAuthorityReserved,
                RegistryErrors.ExternalIdExists, RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased);

        people
            .MapPost("/{personId:guid}/external-identifiers/{identifierId:guid}/end", (
                    Guid personId,
                    Guid identifierId,
                    EndExternalIdentifierRequest? request,
                    EndExternalIdentifierHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, identifierId, request ?? new(), ifMatch, cancellationToken)))
            .WithName("EndPersonExternalIdentifier")
            .WithSummary("End a person's external identifier")
            .WithDescription(
                "Ends an identifier, whatever the person's status: it is kept with its `validTo`, and its value is never " +
                "given to anyone else. The body is optional: `validTo` defaults to today (the tenant's date) and can't be " +
                "after it or before `validFrom` (PERSON_EXTERNAL_ID_VALIDITY_INVALID); `reason` (1–500 characters) is kept " +
                "permanently in the audit trail: don't include personal data. An identifier already ended is " +
                $"PERSON_EXTERNAL_ID_ENDED. {_versionNote}")
            .RequireAction(RegistryActions.PersonExternalIdEnd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<EndExternalIdentifierRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.ExternalIdNotFound,
                RegistryErrors.ExternalIdEnded, RegistryErrors.ExternalIdValidityInvalid);
    }
}
