using FluentValidation;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class GetTenantCapabilitiesRequestValidator : AbstractValidator<GetTenantCapabilitiesRequest>
{
    public GetTenantCapabilitiesRequestValidator()
    {
        RuleFor(request => request.ScopeType)
            .Must(type => type is null || GetTenantCapabilitiesRequest.TryParseScopeType(type, out _))
            .WithMessage($"'scopeType' must be one of: {string.Join(", ", GetTenantCapabilitiesRequest.ScopeTypes)}.");
        RuleFor(request => request.ScopeId)
            .Must(id => id is null || GetTenantCapabilitiesRequest.TryParseScopeId(id, out _))
            .WithMessage("'scopeId' must be a non-empty UUID.");
        RuleFor(request => request)
            .Must(request => (request.ScopeType is null) == (request.ScopeId is null))
            .WithName("scopeType")
            .WithMessage("'scopeType' and 'scopeId' go together: send both or neither.");
    }
}
