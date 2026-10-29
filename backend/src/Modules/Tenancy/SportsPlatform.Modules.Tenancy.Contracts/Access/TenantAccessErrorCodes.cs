namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>
/// The codes of step 1's refusals that other modules report without calling it (the capabilities list, S04 D14). The
/// errors themselves are declared in <c>TenancyErrors</c> (the catalogue reads module assemblies only).
/// </summary>
public static class TenantAccessErrorCodes
{
    /// <summary>A tenant action that isn't available when read-only, on a <c>READ_ONLY</c> tenant (S04 D8).</summary>
    public const string ReadOnly = "TENANT_READ_ONLY";
}
