using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;

namespace SportsPlatform.Modules.Registry.Features.People.CreatePerson;

internal static class CreatePersonEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapPost("", HandleAsync)
        .WithName("CreatePerson")
        .WithSummary("Add a person after a duplicate check")
        .WithDescription(
            "Adds an ACTIVE person to the tenant named by `X-Tenant-Id`. Run the duplicate check first with the same " +
            "names, date of birth, nationalities and external identifiers, and send its `checkToken`: without one, or " +
            "with one that is expired (30 minutes), for other details or another user, the create is " +
            "PERSON_DUPLICATE_CHECK_REQUIRED. The check runs again: an external identifier another person holds is " +
            "PERSON_EXTERNAL_ID_EXISTS; a person the check didn't answer (added since) is PERSON_DUPLICATE_CHECK_STALE " +
            "(check again); people it answered need `overrideReason` (PERSON_DUPLICATE_OVERRIDE_REQUIRED), and each is " +
            "then remembered as not the same person, so the check doesn't show the pair again. A first or last name " +
            "without a letter or a digit is PERSON_NAME_INVALID; a date of birth after the tenant's date is " +
            "PERSON_DATE_OF_BIRTH_IN_FUTURE; the authorities NATIONAL_ID, NIN and PASSPORT are " +
            "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED. The reason is kept permanently on the decisions and in the audit " +
            "trail: don't include personal data. Answers the person as GET /people/{personId} does.")
        .RequireAction(RegistryActions.PersonCreate)
        .ValidateRequest<CreatePersonRequest>(CommonErrors.InvalidRequest)
        .ReturnsETag()
        .ProducesErrors(
            RegistryErrors.NameInvalid, RegistryErrors.DateOfBirthInFuture, RegistryErrors.ExternalIdAuthorityReserved,
            RegistryErrors.DuplicateCheckRequired, RegistryErrors.ExternalIdExists, RegistryErrors.DuplicateCheckStale,
            RegistryErrors.DuplicateOverrideRequired);

    public static string PathOf(Guid personId) => $"/api/admin/v1/people/{personId}";

    private static async Task<Results<Created<PersonResponse>, ProblemHttpResult>> HandleAsync(
        CreatePersonRequest request,
        CreatePersonHandler handler,
        GetPersonHandler read,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // The request carries a date of birth: neither it nor the answer is cached.
        response.Headers.CacheControl = "no-store";
        var (personId, refusal) = await handler.HandleAsync(request, cancellationToken);
        if (refusal is not null)
        {
            return refusal.ToProblem();
        }

        // Saved and committed: the read is not cancelled with the request.
        var person = await read.HandleAsync(personId!.Value, CancellationToken.None);
        person.Value.SetETag(response);
        return TypedResults.Created(PathOf(person.Value.Id), person.Value);
    }
}
