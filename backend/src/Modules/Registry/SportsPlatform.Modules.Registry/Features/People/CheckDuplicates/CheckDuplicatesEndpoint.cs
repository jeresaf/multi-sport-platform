using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

internal static class CheckDuplicatesEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapPost("/duplicate-check", HandleAsync)
        .WithName("CheckPersonDuplicates")
        .WithSummary("Check a person against the registry for duplicates")
        .WithDescription(
            "Compares a new or changed person with the people of the tenant named by `X-Tenant-Id`, whatever their " +
            "status, and answers those who may be the same person, strongest first, at most 20. `EXACT_IDENTIFIER`: an " +
            "external identifier with the same authority and value. `LIKELY`: the same date of birth and the same names " +
            "in any order, or names at least 80 % similar. `POSSIBLE`: names at least 60 % similar and a date of birth " +
            "missing, birth years within one year or a nationality in common (primary or secondary, on either side); the same names with another date of birth; " +
            "or the same date of birth and last name. Names are also compared with aliases. `evidence` says why; it " +
            "never carries the other person's date of birth. With `personId` (a person being changed), that person and " +
            "everyone already decided \"not the same person\" as them are left out. Nothing is saved; `checkToken` " +
            "(valid 30 minutes, for these details and this user) is what creating the person needs. A first or last " +
            "name without a letter or a digit is PERSON_NAME_INVALID; a date of birth after the tenant's date is " +
            "PERSON_DATE_OF_BIRTH_IN_FUTURE; the authorities NATIONAL_ID, NIN and PASSPORT are " +
            "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED. Nationalities are active countries of the reference data.")
        .RequireAction(RegistryActions.PersonDuplicateCheck)
        .ValidateRequest<CheckDuplicatesRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(
            RegistryErrors.NameInvalid, RegistryErrors.DateOfBirthInFuture, RegistryErrors.ExternalIdAuthorityReserved,
            RegistryErrors.PersonNotFound);

    private static async Task<Results<Ok<CheckDuplicatesResponse>, ProblemHttpResult>> HandleAsync(
        CheckDuplicatesRequest request,
        CheckDuplicatesHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // The answer names people the caller typed a date of birth for: never cached.
        response.Headers.CacheControl = "no-store";
        var (answer, refusal) = await handler.HandleAsync(request, cancellationToken);
        if (answer is not null)
        {
            return TypedResults.Ok(answer);
        }

        return refusal!.ToProblem();
    }
}
