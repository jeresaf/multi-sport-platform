using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

internal static class RedriveOutboxMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder platform) => platform
        .MapPost("/diagnostics/outbox/{module}/{messageId:guid}/redrive", HandleAsync)
        .WithName("RedriveOutboxMessage")
        .WithTags("Diagnostics")
        .WithSummary("Re-drive a failed outbox message")
        .WithDescription(
            "Moves one FAILED message back to PENDING with its attempts reset; the processor delivers it on its next poll. " +
            "Fix the cause first: a message that fails again ends FAILED again. " +
            "The reason is kept permanently in the audit trail: don't include personal data.")
        .RequireAction(PlatformActions.OutboxRedrive)
        .ValidateRequest<RedriveOutboxMessageRequest>(CommonErrors.ReasonRequired)
        .ProducesErrors(PlatformErrors.OutboxMessageNotFound, PlatformErrors.OutboxMessageNotFailed);

    private static async Task<Results<Ok<OutboxMessageResponse>, ProblemHttpResult>> HandleAsync(
        string module,
        Guid messageId,
        RedriveOutboxMessageRequest request,
        RedriveOutboxMessageHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(module, messageId, request.Reason!.Trim(), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
