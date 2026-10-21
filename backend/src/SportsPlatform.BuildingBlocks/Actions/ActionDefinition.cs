namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Who an action applies to (docs/architecture/authorization.md).</summary>
public enum ActionScope
{
    Platform,
    Tenant,

    /// <summary>The caller's own account (S03 D2): granted to every authenticated user, under <c>/api/account</c> only.</summary>
    Account,
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
        bool idempotent = false,
        bool allowsAnonymous = false,
        bool availableWhenBlocked = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(since);
        if (entitlement is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entitlement);
        }

        Code = new ActionCode(code);
        ScopableBy = [.. (scopableBy ?? []).Distinct().Order()];

        // Platform roles and accounts are not scoped; every tenant action can at least be granted tenant-wide (TENANT_ADMIN).
        if (scope != ActionScope.Tenant && ScopableBy.Count > 0)
        {
            throw new ArgumentException($"{scope} action '{code}' cannot be scopable by tenant assignment scopes.", nameof(scopableBy));
        }

        if (scope == ActionScope.Tenant && !ScopableBy.Contains(AssignmentScope.Tenant))
        {
            throw new ArgumentException($"Tenant action '{code}' must be scopable by {nameof(AssignmentScope.Tenant)}.", nameof(scopableBy));
        }

        // Only the caller's own account can be worked on before signing in (S03 D2): sign-in, password reset, invitations.
        if (allowsAnonymous && scope != ActionScope.Account)
        {
            throw new ArgumentException($"Only account actions can allow anonymous callers ('{code}' is {scope}).", nameof(allowsAnonymous));
        }

        // Only a tenant's own work is refused while it is blocked; the exception is for its owner (S03 D11).
        if (availableWhenBlocked && scope != ActionScope.Tenant)
        {
            throw new ArgumentException($"Only tenant actions can be available when blocked ('{code}' is {scope}).", nameof(availableWhenBlocked));
        }

        Description = description;
        Scope = scope;
        Audit = audit;
        Since = since;
        Entitlement = entitlement;
        Idempotent = idempotent;
        AllowsAnonymous = allowsAnonymous;
        AvailableWhenBlocked = availableWhenBlocked;
    }

    public ActionCode Code { get; }

    /// <summary>Shown in role editors and the generated catalogue.</summary>
    public string Description { get; }

    public ActionScope Scope { get; }

    /// <summary>Assignment scopes that can grant the action; empty for platform and account actions.</summary>
    public IReadOnlyList<AssignmentScope> ScopableBy { get; }

    public AuditRequirement Audit { get; }

    /// <summary>
    /// The entitlement this tenant action needs, if any (S04 D1, D10): a code declared in Tenancy's
    /// <c>TenancyEntitlements</c>. Tenant actions only; a quota is counted by the handler, not enabled.
    /// </summary>
    public string? Entitlement { get; }

    /// <summary>Whether the request must carry an <c>Idempotency-Key</c>.</summary>
    public bool Idempotent { get; }

    /// <summary>Whether a caller who is not signed in may perform it (account actions only); otherwise anonymous is 401.</summary>
    public bool AllowsAnonymous { get; }

    /// <summary>
    /// Whether a tenant action passes step 1 while the tenant is <c>BLOCKED</c> (suspended or archived); every other
    /// tenant action is <c>TENANT_BLOCKED</c> then. Only <c>TENANT_OWNER</c> holds such actions (S03 D11).
    /// </summary>
    public bool AvailableWhenBlocked { get; }

    /// <summary>The slice that introduced the action, e.g. <c>S01</c>.</summary>
    public string Since { get; }

    public override string ToString() => Code.Value;
}
