using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

internal static class ListOrganisationsEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapGet("", HandleAsync)
        .WithName("ListOrganisations")
        .WithSummary("List the tenant's organisations")
        .WithDescription(
            "The organisations of the tenant named by `X-Tenant-Id`, sorted by display name (ignoring case, accents and " +
            "punctuation) and ID. `q` matches the display name or one of its words by prefix and by similarity, and the " +
            "legal or short name by prefix (word order and punctuation aside). `type` keeps organisations of that type; " +
            "`status` filters by status (without it, archived organisations are left out). `limit` is 1–100 (25). " +
            "Available while the tenant is read-only.")
        .RequireAction(RegistryActions.OrganisationRead)
        .ValidateRequest<ListOrganisationsRequest>(CommonErrors.InvalidQuery)
        .DocumentsQueryValues("type", ListOrganisationsRequest.Types)
        .DocumentsQueryValues("status", ListOrganisationsRequest.Statuses)
        .DocumentsQueryInteger("limit", 1, ListOrganisationsRequest.MaxLimit, ListOrganisationsRequest.DefaultLimit);

    private static async Task<Ok<ListOrganisationsResponse>> HandleAsync(
        [AsParameters] ListOrganisationsRequest request,
        ListOrganisationsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
