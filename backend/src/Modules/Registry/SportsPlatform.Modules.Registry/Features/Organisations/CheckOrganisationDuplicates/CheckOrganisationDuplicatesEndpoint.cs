using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CheckOrganisationDuplicates;

internal static class CheckOrganisationDuplicatesEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapPost("/duplicate-check", HandleAsync)
        .WithName("CheckOrganisationDuplicates")
        .WithSummary("Check an organisation against the registry for duplicates")
        .WithDescription(
            "Compares a new organisation's names, type and city with the organisations of the tenant named by " +
            "`X-Tenant-Id`, archived ones included (see `status`), and answers those that look like it, strongest first, " +
            "at most 10. Only organisations of the same type or in the same city are compared (no city never matches). " +
            "`LIKELY`: one of the names (display, legal or short) is the same as one of the other's (the same words in " +
            "any order; case, accents and punctuation aside). `POSSIBLE`: display names at least 70 % similar. " +
            "`evidence` says why. Nothing is saved: the create runs the same check, and refuses with " +
            "ORGANISATION_DUPLICATE_REVIEW_REQUIRED until it is sent with `duplicatesReviewed`. A display name without a " +
            "letter or a digit is ORGANISATION_NAME_INVALID.")
        .RequireAction(RegistryActions.OrganisationDuplicateCheck)
        .ValidateRequest<CheckOrganisationDuplicatesRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(RegistryErrors.OrganisationNameInvalid);

    private static async Task<Results<Ok<CheckOrganisationDuplicatesResponse>, ProblemHttpResult>> HandleAsync(
        CheckOrganisationDuplicatesRequest request,
        CheckOrganisationDuplicatesHandler handler,
        CancellationToken cancellationToken)
    {
        var (answer, refusal) = await handler.HandleAsync(request, cancellationToken);
        return answer is not null ? TypedResults.Ok(answer) : refusal!.ToProblem();
    }
}
