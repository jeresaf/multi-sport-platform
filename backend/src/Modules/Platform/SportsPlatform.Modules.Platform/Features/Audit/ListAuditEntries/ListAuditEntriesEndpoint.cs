using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

internal static class ListAuditEntriesEndpoint
{
    public static void Map(IEndpointRouteBuilder platform) => platform
        .MapGet("/audit-entries", HandleAsync)
        .WithName("ListAuditEntries")
        .WithTags("Audit")
        .WithSummary("List audit entries")
        .WithDescription(
            "Audit entries of every tenant, newest first. Filter by `tenantId`, `actorId`, `action` (an action code), " +
            "`entityType` and `entityId` (`entityId` needs `entityType`), `correlationId`, `outcome` (SUCCESS, FAILURE or " +
            "DENIED), and `from` (inclusive) / `to` (exclusive), ISO 8601 instants with an offset (encode `+` as `%2B`); " +
            "`limit` is 1–100 (25). Entries recorded through a module's outbox arrive shortly after the action, so one can " +
            "appear before a page already read. Every page read is itself audited: don't poll this list.")
        .RequireAction(PlatformActions.AuditRead)
        .ValidateRequest<ListAuditEntriesRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<ListAuditEntriesResponse>> HandleAsync(
        [AsParameters] ListAuditEntriesRequest request,
        ListAuditEntriesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
