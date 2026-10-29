using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity.Features.Capabilities;

/// <summary>
/// The platform action codes the caller may perform, in ordinal order; <paramref name="Scope"/> is always null (kept so
/// the platform list has the shape it had in S03). The tenant list is <see cref="GetTenantCapabilities.TenantCapabilitiesResponse"/>.
/// </summary>
public sealed record CapabilitiesResponse(IReadOnlyList<string> Actions, CapabilityScopeResponse? Scope);

/// <summary>The resource the capabilities were asked about.</summary>
public sealed record CapabilityScopeResponse(AssignmentScope Type, Guid Id);
