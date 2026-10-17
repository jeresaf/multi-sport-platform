using SportsPlatform.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModules(builder.Configuration, ModuleRegistry.All);

var app = builder.Build();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("HealthLive");

app.MapModules(ModuleRegistry.All);

app.Run();

/// <summary>Entry point; public so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
