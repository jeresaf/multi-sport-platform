using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity.Domain.Roles;

/// <summary>Whom a role is for: platform staff (no tenant) or a tenant's members.</summary>
public enum RoleScope
{
    Platform,
    Tenant,
}

/// <summary>
/// A system role (S03 D9): its code, which assignments may hold it, and the action codes it grants. Actions are named
/// by code because their definitions live in other modules' assemblies (AR-02); the Host checks every code against the
/// action catalogue at startup.
/// </summary>
public sealed class RoleDefinition
{
    public RoleDefinition(
        string code,
        RoleScope scope,
        string description,
        IReadOnlyList<string> actions,
        IReadOnlyList<AssignmentScope>? assignableAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(actions);

        Code = code;
        Scope = scope;
        Description = description;
        Actions = new SortedSet<string>(actions, StringComparer.Ordinal);
        AssignableAt = [.. (assignableAt ?? []).Distinct().Order()];
    }

    /// <summary>Stored on assignments, e.g. <c>TENANT_ADMIN</c>.</summary>
    public string Code { get; }

    public RoleScope Scope { get; }

    /// <summary>Shown in the generated role catalogue.</summary>
    public string Description { get; }

    /// <summary>The action codes the role grants, in ordinal order.</summary>
    public IReadOnlySet<string> Actions { get; }

    /// <summary>The scopes a tenant role can be assigned at (authorization.md, "typical scope"); empty for platform roles.</summary>
    public IReadOnlyList<AssignmentScope> AssignableAt { get; }

    public bool Grants(ActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Actions.Contains(action.Code.Value);
    }
}
