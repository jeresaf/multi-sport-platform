using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Modules.Identity.Domain.Roles;
using PlatformRoleCodes = SportsPlatform.Modules.Identity.Domain.PlatformRoles.PlatformRoles;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// Startup check of the roles defined in code (S03 D9): every action a role grants is declared in the catalogue and
/// fits the role (platform roles only platform actions; tenant roles only tenant actions that one of their assignment
/// scopes can grant), role codes are unique, and the platform roles are the ones the database accepts.
/// </summary>
public static class RoleRules
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<RoleDefinition> roles, IReadOnlyList<ActionCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(catalogue);

        var actions = catalogue.ToDictionary(entry => entry.Action.Code.Value, entry => entry.Action, StringComparer.Ordinal);
        var violations = new List<string>();

        violations.AddRange(roles.GroupBy(role => role.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"Role {group.Key} is defined more than once."));

        var platformRoles = roles.Where(role => role.Scope == RoleScope.Platform).Select(role => role.Code).ToHashSet(StringComparer.Ordinal);
        violations.AddRange(platformRoles.Except(PlatformRoleCodes.All, StringComparer.Ordinal)
            .Select(code => $"Platform role {code} is not in PlatformRoles, so it can't be assigned."));
        violations.AddRange(PlatformRoleCodes.All.Except(platformRoles, StringComparer.Ordinal)
            .Select(code => $"Platform role {code} can be assigned but has no definition."));

        foreach (var role in roles)
        {
            if (role.Scope == RoleScope.Platform && role.AssignableAt.Count > 0)
            {
                violations.Add($"Platform role {role.Code} can't be assigned at tenant scopes.");
            }

            if (role.Scope == RoleScope.Tenant && role.AssignableAt.Count == 0)
            {
                violations.Add($"Tenant role {role.Code} names no scope it can be assigned at.");
            }

            foreach (var code in role.Actions)
            {
                if (!actions.TryGetValue(code, out var action))
                {
                    violations.Add($"Role {role.Code} grants '{code}', which is not declared in a module's *Actions class.");
                }
                else if (ExpectedScope(role.Scope) != action.Scope)
                {
                    violations.Add($"{role.Scope} role {role.Code} grants '{code}', a {action.Scope} action.");
                }
                else if (role.Scope == RoleScope.Tenant && !role.AssignableAt.Any(action.ScopableBy.Contains))
                {
                    violations.Add($"Role {role.Code} grants '{code}', which none of its assignment scopes can grant.");
                }
            }
        }

        return violations;
    }

    /// <summary>Throws when any role breaks the rules, listing every violation.</summary>
    public static void EnsureRolesGrantDeclaredActions(this WebApplication app, IReadOnlyList<ActionCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(app);

        var violations = Validate(app.Services.GetRequiredService<RoleCatalogue>().Roles, catalogue);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException("Every role must grant declared actions of its own scope:\n" + string.Join("\n", violations));
        }
    }

    private static ActionScope ExpectedScope(RoleScope scope) => scope switch
    {
        RoleScope.Platform => ActionScope.Platform,
        RoleScope.Tenant => ActionScope.Tenant,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };
}
