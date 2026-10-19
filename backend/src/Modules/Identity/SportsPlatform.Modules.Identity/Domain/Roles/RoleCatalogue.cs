namespace SportsPlatform.Modules.Identity.Domain.Roles;

/// <summary>
/// The roles the evaluator grants from, registered as a singleton (<see cref="IdentityRoles.All"/>; tests replace it).
/// The Host validates it against the action catalogue at startup.
/// </summary>
public sealed class RoleCatalogue(IReadOnlyList<RoleDefinition> roles)
{
    private readonly Dictionary<string, RoleDefinition> _byCode = roles
        .GroupBy(role => role.Code, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>In declaration order (platform roles first, then authorization.md's order).</summary>
    public IReadOnlyList<RoleDefinition> Roles { get; } = roles;

    public RoleDefinition? Find(string code) => _byCode.GetValueOrDefault(code);
}
