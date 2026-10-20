using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity.Features.Capabilities;

/// <summary>The action codes the caller may perform, in ordinal order; <paramref name="Scope"/> is the resource asked about, if any.</summary>
public sealed record CapabilitiesResponse(IReadOnlyList<string> Actions, CapabilityScopeResponse? Scope);

public sealed record CapabilityScopeResponse(AssignmentScope Type, Guid Id);
