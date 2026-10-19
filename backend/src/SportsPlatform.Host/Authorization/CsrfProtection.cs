using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// S03 D7: every state-changing request to an account, admin or platform endpoint carries <c>X-CSRF: 1</c>, sign-in
/// included. A cross-site form or an image can't set a header, and <c>SameSite=Strict</c> already keeps the session
/// cookie off cross-site requests: the header is defence in depth. After audit, so a refusal of an audited action is
/// audited as DENIED; before tenant resolution and authorization, so nothing is looked up for a forged request.
/// </summary>
public static class CsrfProtection
{
    public const string HeaderName = "X-CSRF";
    public const string HeaderValue = "1";

    private static readonly string[] _protectedFamilies = ["account", "admin", "platform"];

    /// <summary>GET, HEAD, OPTIONS and TRACE change nothing; every other method must carry the header.</summary>
    public static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    /// <summary>After <c>UseAudit</c> and before <c>UseTenantResolution</c>.</summary>
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) =>
        app.Use(static async (context, next) =>
        {
            // The matched endpoint's family, not the raw path: an unknown route stays 404, a wrong method 405.
            if (!IsSafe(context.Request.Method)
                && context.GetEndpoint() is { } endpoint
                && EndpointActionRules.Family(endpoint) is { } family
                && _protectedFamilies.Contains(family)
                && context.Request.Headers[HeaderName] is not [HeaderValue])
            {
                await AuthorizationErrors.CsrfRequired.ToProblem().ExecuteAsync(context);
                return;
            }

            await next(context);
        });
}
