using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;

/// <summary>
/// <c>POST /people/{personId}/{deactivate|reactivate|archive|unarchive|mark-deceased}</c> (S06 D9): one action each.
/// </summary>
internal static class ChangePersonStatusEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read to get 409 PERSON_CONCURRENCY_CONFLICT if the person changed since. " +
        "Any other change of status is PERSON_STATUS_INVALID. Answers the person as GET /people/{personId} does.";

    private const string _reasonNote =
        "The reason is kept permanently in the status history and the audit trail: don't include personal data.";

    public static void Map(IEndpointRouteBuilder people)
    {
        Map(people, "deactivate", PersonTransition.Deactivate, RegistryActions.PersonDeactivate, "DeactivatePerson",
            "Mark a person inactive", "ACTIVE → INACTIVE.");
        Map(people, "reactivate", PersonTransition.Reactivate, RegistryActions.PersonReactivate, "ReactivatePerson",
            "Make an inactive person active", "INACTIVE → ACTIVE.");
        MapWithReason(people, "archive", PersonTransition.Archive, RegistryActions.PersonArchive, "ArchivePerson",
            "Archive a person",
            "ACTIVE or INACTIVE → ARCHIVED, with a reason. The record and its history stay and can still be corrected; " +
            "it takes no new profile, alias, identifier, consent or photo.");
        MapWithReason(people, "unarchive", PersonTransition.Unarchive, RegistryActions.PersonUnarchive, "UnarchivePerson",
            "Unarchive a person", "ARCHIVED → ACTIVE, with a reason.");
        MapWithReason(people, "mark-deceased", PersonTransition.MarkDeceased, RegistryActions.PersonMarkDeceased,
            "MarkPersonDeceased", "Record that a person has died",
            "ACTIVE or INACTIVE → DECEASED, with a reason. Final: the record and its history stay and can still be corrected.");
    }

    private static void Map(
        IEndpointRouteBuilder people, string path, PersonTransition transition, ActionDefinition action, string name, string summary,
        string description) => people
        .MapPost($"/{{personId:guid}}/{path}", (
                Guid personId,
                ChangePersonStatusRequest? request,
                ChangePersonStatusHandler handler,
                GetPersonHandler read,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            HandleAsync(personId, transition, null, request?.Version, handler, read, httpRequest, response, cancellationToken))
        .WithName(name)
        .WithSummary(summary)
        .WithDescription($"{description} The body is optional. {_versionNote}")
        .RequireAction(action)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(RegistryErrors.PersonNotFound, RegistryErrors.StatusInvalid, RegistryErrors.ConcurrencyConflict);

    private static void MapWithReason(
        IEndpointRouteBuilder people, string path, PersonTransition transition, ActionDefinition action, string name, string summary,
        string description) => people
        .MapPost($"/{{personId:guid}}/{path}", (
                Guid personId,
                ChangePersonStatusWithReasonRequest request,
                ChangePersonStatusHandler handler,
                GetPersonHandler read,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            HandleAsync(personId, transition, request.Reason!.Trim(), request.Version, handler, read, httpRequest, response, cancellationToken))
        .WithName(name)
        .WithSummary(summary)
        .WithDescription($"{description} The body `{{ \"reason\": … }}` is required (no body: VALIDATION_MALFORMED_REQUEST). " +
            $"{_reasonNote} {_versionNote}")
        .RequireAction(action)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ValidateRequest<ChangePersonStatusWithReasonRequest>(CommonErrors.ReasonRequired)
        .ProducesErrors(RegistryErrors.PersonNotFound, RegistryErrors.StatusInvalid, RegistryErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> HandleAsync(
        Guid personId,
        PersonTransition transition,
        string? reason,
        uint? version,
        ChangePersonStatusHandler handler,
        GetPersonHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(personId, transition, reason, version, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var person = await read.HandleAsync(personId, CancellationToken.None);
        person.Value.SetETag(response);
        return TypedResults.Ok(person.Value);
    }
}
