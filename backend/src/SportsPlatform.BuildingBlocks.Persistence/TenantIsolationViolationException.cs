namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>Why <see cref="TenantOwnershipInterceptor"/> refused a save.</summary>
public enum TenantIsolationViolation
{
    /// <summary>A tenant-owned entity was saved without a tenant in the scope.</summary>
    NoTenant,

    /// <summary>A tenant-owned entity of another tenant was added, changed or deleted.</summary>
    OtherTenant,
}

/// <summary>A save that would break tenant isolation: a bug, never an expected failure (no error code).</summary>
public sealed class TenantIsolationViolationException(TenantIsolationViolation violation, string message)
    : InvalidOperationException(message)
{
    public TenantIsolationViolation Violation { get; } = violation;
}
