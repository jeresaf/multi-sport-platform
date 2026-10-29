namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// The codes of the failures <see cref="IEntitlements"/> returns or step 2 answers, for the modules that count quotas to
/// recognise them and for the capabilities list (S04 D14). The errors themselves are declared in <c>TenancyErrors</c> (the catalogue reads module assemblies only); the Host
/// documents <see cref="QuotaExceeded"/> on every tenant action that names a quota.
/// </summary>
public static class EntitlementErrorCodes
{
    /// <summary>Step 2: the action names a boolean or level entitlement the tenant can't use (S04 D10, D11).</summary>
    public const string Required = "ENTITLEMENT_REQUIRED";

    /// <summary>A create that would take the tenant past a quota of its plan or override (S04 D11, D12).</summary>
    public const string QuotaExceeded = "ENTITLEMENT_QUOTA_EXCEEDED";
}
