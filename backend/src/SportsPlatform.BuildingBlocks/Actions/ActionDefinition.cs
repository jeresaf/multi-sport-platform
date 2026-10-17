namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Who an action applies to (docs/architecture/authorization.md).</summary>
public enum ActionScope
{
    Platform,
    Tenant,
}

/// <summary>A role assignment scope that can grant a tenant action.</summary>
public enum AssignmentScope
{
    Tenant,
    Competition,
    Team,
    Contest,
}

/// <summary>Whether performing the action writes an audit entry, and whether it needs a reason.</summary>
public enum AuditRequirement
{
    None,
    Required,
    RequiredWithReason,
}

/// <summary>
/// One business operation in the action catalogue. Each module declares its actions as public static readonly fields of
/// a static <c>&lt;Module&gt;Actions</c> class; the CLI exports them to <c>docs/generated/actions.md</c>. The catalogue
/// adds the declaring module.
/// </summary>
public sealed class ActionDefinition
{
    public ActionDefinition(
        string code,
        string description,
        ActionScope scope,
        AuditRequirement audit,
        string since,
        IReadOnlyList<AssignmentScope>? scopableBy = null,
        string? entitlement = null,
        bool idempotent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(since);
        if (entitlement is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entitlement);
        }

        Code = new ActionCode(code);
        ScopableBy = [.. (scopableBy ?? []).Distinct().Order()];

        // Platform roles are not scoped; every tenant action can at least be granted tenant-wide (TENANT_ADMIN).
        if (scope == ActionScope.Platform && ScopableBy.Count > 0)
        {
            throw new ArgumentException($"Platform action '{code}' cannot be scopable by tenant assignment scopes.", nameof(scopableBy));
        }

        if (scope == ActionScope.Tenant && !ScopableBy.Contains(AssignmentScope.Tenant))
        {
            throw new ArgumentException($"Tenant action '{code}' must be scopable by {nameof(AssignmentScope.Tenant)}.", nameof(scopableBy));
        }

        Description = description;
        Scope = scope;
        Audit = audit;
        Since = since;
        Entitlement = entitlement;
        Idempotent = idempotent;
    }

    public ActionCode Code { get; }

    /// <summary>Shown in role editors and the generated catalogue.</summary>
    public string Description { get; }

    public ActionScope Scope { get; }

    /// <summary>Assignment scopes that can grant the action; empty for platform actions.</summary>
    public IReadOnlyList<AssignmentScope> ScopableBy { get; }

    public AuditRequirement Audit { get; }

    /// <summary>The entitlement that must be enabled, if any (entitlement codes arrive with S04).</summary>
    public string? Entitlement { get; }

    /// <summary>Whether the request must carry an <c>Idempotency-Key</c>.</summary>
    public bool Idempotent { get; }

    /// <summary>The slice that introduced the action, e.g. <c>S01</c>.</summary>
    public string Since { get; }

    public override string ToString() => Code.Value;
}
