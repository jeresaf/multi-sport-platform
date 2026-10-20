namespace SportsPlatform.Host.Tenancy;

public static class TenantResolution
{
    /// <summary>After <c>UseAudit</c> and before <c>UseActionAuthorization</c> (S02 D8).</summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
