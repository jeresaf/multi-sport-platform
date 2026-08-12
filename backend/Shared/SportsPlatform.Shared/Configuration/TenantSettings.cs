namespace SportsPlatform.Shared.Configuration;

public class TenantSettings
{
    public string DefaultConnectionString { get; set; } = string.Empty;
    public bool EnableMultiTenancy { get; set; } = true;
    public string TenantResolutionStrategy { get; set; } = "Header"; // Header, Host, Claim
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";
}