using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

/// <summary>
/// The optional resource, as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>): both parameters or
/// neither. The tenant itself is the default, so <c>TENANT</c> is not a resource type here.
/// </summary>
public sealed record GetTenantCapabilitiesRequest(
    [FromQuery(Name = "scopeType")] string? ScopeType,
    [FromQuery(Name = "scopeId")] string? ScopeId)
{
    // In declaration order (COMPETITION, TEAM, CONTEST): the validator's message and OpenAPI's enum list them so.
    private static readonly (string Name, AssignmentScope Type)[] _ordered = [.. Enum.GetValues<AssignmentScope>()
        .Where(type => type != AssignmentScope.Tenant)
        .Select(type => (MessageSerialization.EnumNamingPolicy.ConvertName(type.ToString()), type))];

    private static readonly Dictionary<string, AssignmentScope> _types =
        _ordered.ToDictionary(entry => entry.Name, entry => entry.Type, StringComparer.Ordinal);

    public static IReadOnlyList<string> ScopeTypes { get; } = [.. _ordered.Select(entry => entry.Name)];

    public static bool TryParseScopeType(string value, out AssignmentScope type) => _types.TryGetValue(value, out type);

    public static bool TryParseScopeId(string value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;

    /// <summary>The resource, or null for the tenant; call it only after the validator accepted the request.</summary>
    public ScopeRef? ToScope()
    {
        if (ScopeType is null && ScopeId is null)
        {
            return null;
        }

        return ScopeType is not null && TryParseScopeType(ScopeType, out var type) && ScopeId is not null && TryParseScopeId(ScopeId, out var id)
            ? new ScopeRef(type, id)
            : throw new InvalidOperationException("Validate the request first.");
    }
}
