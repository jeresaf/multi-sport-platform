using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Host;
using SportsPlatform.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSportsPlatform(builder.Configuration);

var app = builder.Build();

// Development convenience only; production applies migrations with the CLI `migrate` command.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.GetRequiredService<MigrationRunner>().RunAsync();
}

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("HealthLive");

app.MapModules(ModuleRegistry.All);

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
