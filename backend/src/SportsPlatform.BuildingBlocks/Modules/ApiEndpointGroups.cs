using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.BuildingBlocks.Modules;

/// <summary>The API families a module maps endpoints into; the Host owns their URL prefixes.</summary>
public sealed record ApiEndpointGroups(
    IEndpointRouteBuilder Public, IEndpointRouteBuilder Admin, IEndpointRouteBuilder Platform, IEndpointRouteBuilder Account);
