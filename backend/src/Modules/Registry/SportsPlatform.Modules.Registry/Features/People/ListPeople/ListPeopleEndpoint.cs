using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.People.ListPeople;

internal static class ListPeopleEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapGet("", HandleAsync)
        .WithName("ListPeople")
        .WithSummary("List the tenant's people")
        .WithDescription(
            "The people of the tenant named by `X-Tenant-Id`, sorted by last name, first name and ID. `q` matches a name " +
            "or an alias by prefix or similarity (in any order: \"Ssekandi John\" finds John Ssekandi) and an external " +
            "identifier's value exactly. Filter by `status` (without it, archived people are left out) and `ageStatus` " +
            "(on the tenant's date; UNKNOWN: no date of birth); `profile` and `variant` keep people with a profile of that " +
            "kind and sport variant that isn't archived: an athlete profile's variant or one of a staff profile's capabilities " +
            "(a variant without people answers an empty page). `limit` is " +
            "1–100 (25). Items carry the birth year, never the date of birth. `isMinor` is true unless the person is known to be an adult (`ageStatus` UNKNOWN: true).")
        .RequireAction(RegistryActions.PersonRead)
        .ValidateRequest<ListPeopleRequest>(CommonErrors.InvalidQuery)
        .DocumentsQueryValues("status", ListPeopleRequest.Statuses)
        .DocumentsQueryValues("ageStatus", ListPeopleRequest.AgeStatuses)
        .DocumentsQueryValues("profile", ListPeopleRequest.Profiles)
        .DocumentsQueryInteger("limit", 1);

    private static async Task<Ok<ListPeopleResponse>> HandleAsync(
        [AsParameters] ListPeopleRequest request,
        ListPeopleHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
