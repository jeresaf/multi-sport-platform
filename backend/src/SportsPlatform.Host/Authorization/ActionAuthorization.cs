namespace SportsPlatform.Host.Authorization;

public static class ActionAuthorization
{
    /// <summary>After <c>UseRouting</c>, so the matched endpoint's metadata is known.</summary>
    public static IApplicationBuilder UseActionAuthorization(this IApplicationBuilder app) =>
        app.UseMiddleware<ActionAuthorizationMiddleware>();
}
