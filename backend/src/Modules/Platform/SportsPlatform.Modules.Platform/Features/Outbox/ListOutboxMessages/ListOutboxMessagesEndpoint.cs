using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

internal static class ListOutboxMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder platform) => platform
        .MapGet("/diagnostics/outbox", HandleAsync)
        .WithName("ListOutboxMessages")
        .WithTags("Diagnostics")
        .WithSummary("List outbox messages")
        .WithDescription(
            "Outbox messages of every module, newest first, without their payloads. Filter by `module` (a module schema) " +
            "and `status` (PENDING, PROCESSED or FAILED); `limit` is 1–100 (25). `heldBackBy` names the earlier message a PENDING one waits for.")
        .RequireAction(PlatformActions.OutboxRead)
        .ProducesErrors(CommonErrors.InvalidQuery);

    private static async Task<Results<Ok<ListOutboxMessagesResponse>, ProblemHttpResult>> HandleAsync(
        string? module,
        string? status,
        string? limit,
        string? cursor,
        OutboxDiagnostics diagnostics,
        ListOutboxMessagesHandler handler,
        CancellationToken cancellationToken)
    {
        var (query, problem) = new ListOutboxMessagesRequest(module, status, limit, cursor).Validate(diagnostics.Modules);
        if (query is null)
        {
            return CommonErrors.InvalidQuery.ToProblem(problem);
        }

        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }
}
