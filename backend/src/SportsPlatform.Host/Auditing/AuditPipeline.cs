namespace SportsPlatform.Host.Auditing;

public static class AuditPipeline
{
    /// <summary>After <c>UseRouting</c> and before <c>UseActionAuthorization</c>, so denials are audited too.</summary>
    public static IApplicationBuilder UseAudit(this IApplicationBuilder app) => app.UseMiddleware<AuditMiddleware>();
}
