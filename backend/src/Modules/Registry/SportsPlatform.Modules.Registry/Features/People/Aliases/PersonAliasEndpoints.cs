using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.Aliases;

/// <summary><c>POST /people/{personId}/aliases</c> and <c>POST /people/{personId}/aliases/{aliasId}/remove</c> (S06 D10).</summary>
internal static class PersonAliasEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since; " +
        "without them, a change to the same person saved at the same moment can still answer it. " +
        "Answers the person as GET /people/{personId} does; its version moves.";

    public static void Map(IEndpointRouteBuilder people)
    {
        people
            .MapPost("/{personId:guid}/aliases", (
                    Guid personId,
                    AddPersonAliasRequest request,
                    AddPersonAliasHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, request, ifMatch, cancellationToken)))
            .WithName("AddPersonAlias")
            .WithSummary("Add an alias to a person")
            .WithDescription(
                "Adds another name the person is known by (`FORMER_NAME`, `NICKNAME` or `SPELLING`): the directory search " +
                "and the duplicate check find the person under it too. No duplicate check runs. A name the person is " +
                "already known by (the same name key as their own name or a current alias: word order, case, accents and " +
                "punctuation aside) is PERSON_ALIAS_EXISTS; a name without a letter or a digit PERSON_NAME_INVALID. An " +
                $"archived or deceased person takes no new alias (PERSON_ARCHIVED, PERSON_DECEASED). {_versionNote}")
            .RequireAction(RegistryActions.PersonAliasAdd)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<AddPersonAliasRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict, RegistryErrors.NameInvalid,
                RegistryErrors.AliasExists, RegistryErrors.PersonArchived, RegistryErrors.PersonDeceased);

        people
            .MapPost("/{personId:guid}/aliases/{aliasId:guid}/remove", (
                    Guid personId,
                    Guid aliasId,
                    RemovePersonAliasRequest request,
                    RemovePersonAliasHandler handler,
                    GetPersonHandler read,
                    HttpRequest httpRequest,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                PersonChanges.AnswerAsync(personId, read, httpRequest, response,
                    ifMatch => handler.HandleAsync(personId, aliasId, request, ifMatch, cancellationToken)))
            .WithName("RemovePersonAlias")
            .WithSummary("Remove a person's alias")
            .WithDescription(
                "Removes an alias, whatever the person's status, with a reason (body `{ \"reason\": … }`, required): the " +
                "alias is kept with who removed it, when and why, and is no longer shown, searched or matched. The reason " +
                "is kept permanently on the alias and in the audit trail: don't include personal data. An alias already " +
                $"removed is PERSON_ALIAS_REMOVED. {_versionNote}")
            .RequireAction(RegistryActions.PersonAliasRemove)
            .AcceptsIfMatch()
            .ReturnsETag()
            .ValidateRequest<RemovePersonAliasRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(
                RegistryErrors.PersonNotFound, RegistryErrors.PersonConcurrencyConflict, RegistryErrors.AliasNotFound,
                RegistryErrors.AliasRemoved);
    }
}
