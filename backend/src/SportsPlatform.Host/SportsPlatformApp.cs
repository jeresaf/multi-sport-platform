using SportsPlatform.Host.Actors;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Auditing;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Health;
using SportsPlatform.Host.Modules;
using SportsPlatform.Host.Observability;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.Host.Tenancy;

namespace SportsPlatform.Host;

/// <summary>
/// The API's services, pipeline and endpoints. The API runs it; the CLI builds it without running it to export the
/// OpenAPI document, so both see exactly the same endpoints.
/// </summary>
public static class SportsPlatformApp
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        AddServices(builder);

        var app = builder.Build();
        UsePipeline(app);

        app.MapHealthEndpoints();
        app.MapApiDocumentation(app.Environment);

        app.MapModules(ModuleRegistry.All);

        // Explicit, and after every Map call: UseEndpoints registers the endpoints with routing (and so with the
        // OpenAPI document) now. With the implicit one that happens only when the app starts, and the CLI exports
        // the document from an app it builds but never starts.
        app.UseEndpoints(static _ => { });

        // Fail closed: the API (and the CLI's export) won't build with an endpoint that declares no action, or a role
        // that grants an undeclared action or one of another scope.
        var actions = ActionCatalogue.Build(ModuleRegistry.All);
        app.EnsureEndpointsDeclareActions(actions);
        app.EnsureRolesGrantDeclaredActions(actions);
        app.EnsureRequestValidators();

        return app;
    }

    public static void AddServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddObservability();
        builder.Services.AddSportsPlatform(builder.Configuration);
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddHealthEndpoints();
        builder.Services.AddApiDocumentation();
        builder.Services.AddForwardedHost(builder.Configuration);
    }

    /// <summary>
    /// The middleware, in order (docs/architecture/authorization.md, S02 D8, S03 D7): forwarded host → logging and
    /// correlation → routing → actor → rate limits (S03 D15; before audit, so a flood writes no audit rows) → audit → CSRF → tenant resolution → authorization → endpoint. Tests that need their own endpoints run this too.
    /// </summary>
    public static void UsePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHost();
        app.UseRequestLogging();
        app.UseApiConventions();
        app.UseRouting();
        app.UseCurrentActor();
        app.UseRateLimiter();
        app.UseAudit();
        app.UseCsrfProtection();
        app.UseTenantResolution();
        app.UseActionAuthorization();
    }
}
