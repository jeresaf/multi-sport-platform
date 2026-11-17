using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.UpdatePerson;

internal static class UpdatePersonEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapPut("/{personId:guid}", HandleAsync)
        .WithName("UpdatePerson")
        .WithSummary("Change a person's biodata")
        .WithDescription(
            "Replaces the biodata of a person of the tenant named by `X-Tenant-Id`, whatever their status: a field left " +
            "out is cleared. External identifiers and aliases have their own actions; the status changes through the " +
            "lifecycle actions. When the names (as the duplicate check compares them: case, accents and punctuation " +
            "aside), the date of birth or the nationalities change, run the duplicate check for the new details with " +
            "`personId` and no external identifiers, and send its `checkToken`; then it works as a create does: " +
            "PERSON_DUPLICATE_CHECK_REQUIRED, PERSON_DUPLICATE_CHECK_STALE, and `overrideReason` for the people it found " +
            "(PERSON_DUPLICATE_OVERRIDE_REQUIRED), each then remembered as not the same person. People already decided " +
            "not the same person as this one aren't found again. Send `version` or `If-Match` from the last read to get " +
            "409 PERSON_CONCURRENCY_CONFLICT if the person changed since. The reason is kept permanently on the " +
            "decisions and in the audit trail: don't include personal data. Answers the person as GET " +
            "/people/{personId} does.")
        .RequireAction(RegistryActions.PersonUpdate)
        .ValidateRequest<UpdatePersonRequest>(CommonErrors.InvalidRequest)
        .AcceptsIfMatch()
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.PersonNotFound, RegistryErrors.ConcurrencyConflict, RegistryErrors.NameInvalid,
            RegistryErrors.DateOfBirthInFuture, RegistryErrors.DuplicateCheckRequired, RegistryErrors.DuplicateCheckStale,
            RegistryErrors.DuplicateOverrideRequired);

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> HandleAsync(
        Guid personId,
        UpdatePersonRequest request,
        UpdatePersonHandler handler,
        GetPersonHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // The request carries a date of birth: neither it nor the answer is cached.
        response.Headers.CacheControl = "no-store";
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await handler.HandleAsync(personId, request, ifMatch, cancellationToken) is { } refusal)
        {
            return refusal.ToProblem();
        }

        // Saved and committed: the read is not cancelled with the request.
        var person = await read.HandleAsync(personId, CancellationToken.None);
        person.Value.SetETag(response);
        return TypedResults.Ok(person.Value);
    }
}
