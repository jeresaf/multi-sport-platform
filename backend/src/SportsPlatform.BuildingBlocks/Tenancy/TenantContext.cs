namespace SportsPlatform.BuildingBlocks.Tenancy;

/// <summary>
/// The scoped <see cref="ITenantContext"/>. Set once per scope (S02 D1): before the first connection that reads or
/// writes tenant data opens, because the RLS value is written when a connection opens. The outbox processor sets it
/// from the event envelope before a consumer runs.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private bool _isSet;

    public Guid? TenantId { get; private set; }

    /// <summary>Sets the scope's tenant. Setting the same value again is a no-op; a different value is a bug and throws.</summary>
    public void Set(Guid? tenantId)
    {
        if (_isSet && TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "The tenant of this scope is already set to another value; a scope runs under one tenant.");
        }

        TenantId = tenantId;
        _isSet = true;
    }
}
