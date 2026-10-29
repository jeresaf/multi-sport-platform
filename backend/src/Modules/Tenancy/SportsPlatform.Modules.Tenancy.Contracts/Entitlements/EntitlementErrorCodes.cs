namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// The codes of the failures <see cref="IEntitlements"/> returns, for the modules that count quotas to recognise them.
/// The errors themselves are declared in <c>TenancyErrors</c> (the catalogue reads module assemblies only); the Host
/// documents <see cref="QuotaExceeded"/> on every tenant action that names a quota.
/// </summary>
public static class EntitlementErrorCodes
{
    /// <summary>A create that would take the tenant past a quota of its plan or override (S04 D11, D12).</summary>
    public const string QuotaExceeded = "ENTITLEMENT_QUOTA_EXCEEDED";
}
