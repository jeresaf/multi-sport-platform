using FluentValidation;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Features.Outbox.ListOutboxMessages;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListOutboxMessagesRequestValidator : AbstractValidator<ListOutboxMessagesRequest>
{
    public ListOutboxMessagesRequestValidator(OutboxDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var modules = diagnostics.Modules;

        RuleFor(request => request.Module)
            .Must(module => module is null || modules.Contains(module))
            .WithMessage($"'module' must be one of: {string.Join(", ", modules)}.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListOutboxMessagesRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListOutboxMessagesRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListOutboxMessagesRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListOutboxMessagesRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || OutboxCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
