using Microsoft.AspNetCore.Routing.Patterns;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Catalogues;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// Fail closed: every endpoint declares exactly one of <c>.RequireAction(...)</c> or <c>.AllowPublic()</c>; admin and
/// platform endpoints are never public; a required action is in the catalogue; an endpoint that runs under the route's
/// tenant has a platform action and that route parameter, and a platform route with <c>{tenantId}</c> runs under it. The Host checks this when it is built.
/// </summary>
public static class EndpointActionRules
{
    private const string _tenantRouteValue = "tenantId";

    private static readonly string[] _protectedFamilies = ["admin", "platform"];

    public static IReadOnlyList<string> Validate(IEnumerable<Endpoint> endpoints, IReadOnlyList<ActionCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(catalogue);

        var catalogued = catalogue.Select(entry => entry.Action).ToHashSet(ReferenceEqualityComparer.Instance);
        var violations = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var name = Describe(endpoint);
            var required = endpoint.Metadata.GetOrderedMetadata<RequiresActionMetadata>();
            var isPublic = endpoint.Metadata.GetMetadata<AllowPublicMetadata>() is not null;

            if (required.Count == 0 && !isPublic)
            {
                violations.Add($"{name} declares neither .RequireAction(...) nor .AllowPublic().");
            }
            else if (required.Count > 0 && isPublic)
            {
                violations.Add($"{name} declares both .RequireAction(...) and .AllowPublic().");
            }
            else if (required.Count > 1)
            {
                violations.Add($"{name} declares more than one action ({string.Join(", ", required.Select(r => r.Action.Code))}).");
            }

            if (isPublic && IsProtectedFamily(endpoint))
            {
                violations.Add($"{name} is under /api/admin or /api/platform and cannot be .AllowPublic().");
            }

            if (endpoint.Metadata.GetMetadata<RunsUnderRouteTenantMetadata>() is { } routeTenant
                && (required.Count != 1 || required[0].Action.Scope != ActionScope.Platform
                    || (endpoint as RouteEndpoint)?.RoutePattern.GetParameter(routeTenant.RouteValueName) is null))
            {
                violations.Add($"{name} runs under the route's tenant, so it needs one platform action and a " +
                    $"'{{{routeTenant.RouteValueName}}}' route parameter.");
            }

            // The reverse: a platform route naming a tenant runs under it, or its entries and writes would have none (D2).
            if (endpoint.Metadata.GetMetadata<RunsUnderRouteTenantMetadata>() is null
                && Family(endpoint) is "platform"
                && (endpoint as RouteEndpoint)?.RoutePattern.GetParameter(_tenantRouteValue) is not null)
            {
                violations.Add($"{name} names a tenant in its route, so it must declare .RunsUnderRouteTenant().");
            }

            violations.AddRange(required
                .Where(r => !catalogued.Contains(r.Action))
                .Select(r => $"{name} requires '{r.Action.Code}', which is not declared in a module's *Actions class."));
        }

        return violations;
    }

    /// <summary>Throws when any endpoint breaks the rules, listing every violation.</summary>
    public static void EnsureEndpointsDeclareActions(this IEndpointRouteBuilder endpoints, IReadOnlyList<ActionCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var violations = Validate(endpoints.DataSources.SelectMany(source => source.Endpoints), catalogue);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Every endpoint must declare exactly one of .RequireAction(...) or .AllowPublic():\n" + string.Join("\n", violations));
        }
    }

    // From the parsed segments, not the raw text, so "~/api/admin/..." and patterns built without raw text count too.
    private static bool IsProtectedFamily(Endpoint endpoint) =>
        Family(endpoint) is { } family && _protectedFamilies.Contains(family);

    /// <summary>"admin", "platform", "public", … for <c>/api/{family}/…</c> (lower case); otherwise null.</summary>
    private static string? Family(Endpoint endpoint)
    {
        if (endpoint is not RouteEndpoint route)
        {
            return null;
        }

        var literals = route.RoutePattern.PathSegments
            .Take(2)
            .Select(segment => segment.IsSimple && segment.Parts[0] is RoutePatternLiteralPart literal ? literal.Content : null)
            .ToList();
        return literals.Count == 2 && string.Equals(literals[0], "api", StringComparison.OrdinalIgnoreCase)
            ? literals[1]?.ToLowerInvariant()
            : null;
    }

    private static string Describe(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        var verb = methods is { Count: > 0 } ? string.Join("|", methods) + " " : string.Empty;
        return endpoint is RouteEndpoint { RoutePattern: RoutePattern pattern }
            ? $"{verb}{pattern.RawText}"
            : endpoint.DisplayName ?? "An endpoint";
    }
}
