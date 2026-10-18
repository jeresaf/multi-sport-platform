using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

public sealed record ListOutboxMessagesResponse(IReadOnlyList<OutboxMessageResponse> Items, PageInfo Page);

/// <summary>Lists outbox messages of every module (or one), newest first.</summary>
public sealed class ListOutboxMessagesHandler(OutboxDiagnostics diagnostics)
{
    public async Task<ListOutboxMessagesResponse> HandleAsync(OutboxQuery query, CancellationToken cancellationToken)
    {
        var page = await diagnostics.ListAsync(query, cancellationToken);
        var nextCursor = page.HasMore ? OutboxCursor.Encode(page.Items[^1].Id) : null;
        return new ListOutboxMessagesResponse([.. page.Items.Select(OutboxMessageResponse.From)], new PageInfo(nextCursor, page.HasMore));
    }
}
