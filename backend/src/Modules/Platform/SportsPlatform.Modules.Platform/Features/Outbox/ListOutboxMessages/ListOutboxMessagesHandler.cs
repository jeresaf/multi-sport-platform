using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

public sealed record ListOutboxMessagesResponse(IReadOnlyList<OutboxMessageResponse> Items, PageInfo Page);

/// <summary>
/// Lists outbox messages of every module (or one), newest first. Saves nothing, so the Host writes the audit entry
/// (with the filters) after the response.
/// </summary>
public sealed class ListOutboxMessagesHandler(OutboxDiagnostics diagnostics, AuditScope audit)
{
    public async Task<ListOutboxMessagesResponse> HandleAsync(OutboxQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        audit.Describe(metadata: new ListAuditMetadata(query.Module, query.Status));
        var page = await diagnostics.ListAsync(query, cancellationToken);
        var nextCursor = page.HasMore ? OutboxCursor.Encode(page.Items[^1].Id) : null;
        return new ListOutboxMessagesResponse([.. page.Items.Select(OutboxMessageResponse.From)], new PageInfo(nextCursor, page.HasMore));
    }
}

/// <summary>The filters of the list, as the audit entry records them.</summary>
public sealed record ListAuditMetadata(string? Module, OutboxMessageStatus? Status);
