using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Venues.CheckVenueDuplicates;

internal static class CheckVenueDuplicatesEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapPost("/duplicate-check", HandleAsync)
        .WithName("CheckVenueDuplicates")
        .WithSummary("Check a venue against the registry for duplicates")
        .WithDescription(
            "Compares a new venue's name, city and coordinates with the venues of the tenant named by `X-Tenant-Id`, " +
            "archived ones included (see `status`), and answers those that look like it, strongest first, at most 10. " +
            "`LIKELY`: the same name in the same city (the same words in any order; case, accents and punctuation " +
            "aside), or coordinates within 200 m. `POSSIBLE`: names at least 70 % similar in the same city. `evidence` " +
            "says why. Nothing is saved: the create runs the same check, and refuses with " +
            "VENUE_DUPLICATE_REVIEW_REQUIRED until it is sent with `duplicatesReviewed`. A name without a letter or a " +
            "digit is VENUE_NAME_INVALID.")
        .RequireAction(RegistryActions.VenueDuplicateCheck)
        .ValidateRequest<CheckVenueDuplicatesRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(RegistryErrors.VenueNameInvalid);

    private static async Task<Results<Ok<CheckVenueDuplicatesResponse>, ProblemHttpResult>> HandleAsync(
        CheckVenueDuplicatesRequest request,
        CheckVenueDuplicatesHandler handler,
        CancellationToken cancellationToken)
    {
        var (answer, refusal) = await handler.HandleAsync(request, cancellationToken);
        return answer is not null ? TypedResults.Ok(answer) : refusal!.ToProblem();
    }
}
