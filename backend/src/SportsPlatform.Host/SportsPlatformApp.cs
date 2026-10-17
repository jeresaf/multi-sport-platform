using SportsPlatform.Host.Api;
using SportsPlatform.Host.Health;
using SportsPlatform.Host.Modules;
using SportsPlatform.Host.Observability;
using SportsPlatform.Host.OpenApi;

namespace SportsPlatform.Host;

/// <summary>
/// The API's services, pipeline and endpoints. The API runs it; the CLI builds it without running it to export the
/// OpenAPI document, so both see exactly the same endpoints.
/// </summary>
public static class SportsPlatformApp
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddObservability();
        builder.Services.AddSportsPlatform(builder.Configuration);
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddHealthEndpoints();
        builder.Services.AddApiDocumentation();

        var app = builder.Build();

        app.UseRequestLogging();
        app.UseApiConventions();
        app.UseRouting();

        app.MapHealthEndpoints();
        app.MapApiDocumentation(app.Environment);

        app.MapModules(ModuleRegistry.All);

        // Explicit, and after every Map call: UseEndpoints registers the endpoints with routing (and so with the
        // OpenAPI document) now. With the implicit one that happens only when the app starts, and the CLI exports
        // the document from an app it builds but never starts.
        app.UseEndpoints(static _ => { });

        return app;
    }
}
