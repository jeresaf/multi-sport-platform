using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Teams.CheckTeamDuplicates;

internal static class CheckTeamDuplicatesEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapPost("/duplicate-check", HandleAsync)
        .WithName("CheckTeamDuplicates")
        .WithSummary("Check a team against the registry for duplicates")
        .WithDescription(
            "Compares a new team's names, variant, category and organisation with the teams of the tenant named by " +
            "`X-Tenant-Id`, archived ones included (see `status`), and answers those that look like it, strongest first, at " +
            "most 10. Only teams of the same variant and the same category (gender, age group and level; `level` defaults " +
            "to FIRST) are compared: a club's other categories are other teams. `LIKELY`: the name or short name is the same " +
            "as the other's (the same words in any order; case, accents and punctuation aside). `POSSIBLE`: names at least " +
            "70 % similar, when both belong to the same organisation or either belongs to none. `evidence` says why. Nothing " +
            "is saved: the create runs the same check, and refuses with TEAM_DUPLICATE_REVIEW_REQUIRED until it is sent with " +
            "`duplicatesReviewed`. A name without a letter or a digit is TEAM_NAME_INVALID.")
        .RequireAction(RegistryActions.TeamDuplicateCheck)
        .ValidateRequest<CheckTeamDuplicatesRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(RegistryErrors.TeamNameInvalid);

    private static async Task<Results<Ok<CheckTeamDuplicatesResponse>, ProblemHttpResult>> HandleAsync(
        CheckTeamDuplicatesRequest request,
        CheckTeamDuplicatesHandler handler,
        CancellationToken cancellationToken)
    {
        var (answer, refusal) = await handler.HandleAsync(request, cancellationToken);
        return answer is not null ? TypedResults.Ok(answer) : refusal!.ToProblem();
    }
}
