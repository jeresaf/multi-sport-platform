using SportsPlatform.Host;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSportsPlatform(builder.Configuration);
builder.Services.AddApiConventions(builder.Configuration);

var app = builder.Build();

await StartupMigration.RunIfEnabledAsync(app);

app.UseApiConventions();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("HealthLive");

app.MapModules(ModuleRegistry.All);

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
