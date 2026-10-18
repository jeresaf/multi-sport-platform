using FluentValidation;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListTenantsRequestValidator : AbstractValidator<ListTenantsRequest>
{
    public ListTenantsRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is null || ListTenantsRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListTenantsRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListTenantsRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListTenantsRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || TenantCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
