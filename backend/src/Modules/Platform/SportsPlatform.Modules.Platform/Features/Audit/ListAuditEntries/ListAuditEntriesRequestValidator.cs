using FluentValidation;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;

namespace SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListAuditEntriesRequestValidator : AbstractValidator<ListAuditEntriesRequest>
{
    private const string _instantFormat = "an ISO 8601 instant with an offset, such as 2026-10-17T09:00:00Z";

    public ListAuditEntriesRequestValidator()
    {
        RuleFor(request => request.TenantId).Must(BeIdOrNull).WithMessage("'tenantId' must be a UUID.");
        RuleFor(request => request.ActorId).Must(BeIdOrNull).WithMessage("'actorId' must be a UUID.");
        RuleFor(request => request.Action)
            .Must(action => action is null || ActionCode.IsValid(action))
            .WithMessage("'action' must be an action code, such as platform.outbox.redrive.");
        RuleFor(request => request.EntityType)
            .Must(type => type is null || type.Length is >= 1 and <= AuditRecord.EntityTypeMaxLength)
            .WithMessage($"'entityType' must be 1–{AuditRecord.EntityTypeMaxLength} characters.");
        RuleFor(request => request.EntityId).Must(BeIdOrNull).WithMessage("'entityId' must be a UUID.");
        RuleFor(request => request.EntityId)
            .Must((request, id) => id is null || request.EntityType is not null)
            .WithMessage("'entityId' needs 'entityType'.");
        RuleFor(request => request.CorrelationId)
            .Must(id => id is null || id.Length is >= 1 and <= ListAuditEntriesRequest.CorrelationIdMaxLength)
            .WithMessage($"'correlationId' must be 1–{ListAuditEntriesRequest.CorrelationIdMaxLength} characters.");
        RuleFor(request => request.Outcome)
            .Must(outcome => outcome is null || ListAuditEntriesRequest.TryParseOutcome(outcome, out _))
            .WithMessage($"'outcome' must be one of: {string.Join(", ", ListAuditEntriesRequest.Outcomes)}.");
        RuleFor(request => request.From).Must(BeInstantOrNull).WithMessage($"'from' must be {_instantFormat}.");
        RuleFor(request => request.To).Must(BeInstantOrNull).WithMessage($"'to' must be {_instantFormat}.");
        RuleFor(request => request.To)
            .Must((request, to) => !(ListAuditEntriesRequest.TryParseInstant(request.From ?? string.Empty, out var from)
                && ListAuditEntriesRequest.TryParseInstant(to ?? string.Empty, out var until)) || from < until)
            .WithMessage("'to' must be later than 'from'.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListAuditEntriesRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListAuditEntriesRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || AuditCursor.TryDecode(cursor, out _, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }

    private static bool BeIdOrNull(string? value) => value is null || ListAuditEntriesRequest.TryParseId(value, out _);

    private static bool BeInstantOrNull(string? value) => value is null || ListAuditEntriesRequest.TryParseInstant(value, out _);
}
