using SportsPlatform.Host;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Health;
using SportsPlatform.Host.Modules;
using SportsPlatform.Host.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddSportsPlatform(builder.Configuration);
builder.Services.AddApiConventions(builder.Configuration);
builder.Services.AddHealthEndpoints();

var app = builder.Build();

await StartupMigration.RunIfEnabledAsync(app);

app.UseRequestLogging();
app.UseApiConventions();

app.MapHealthEndpoints();

app.MapModules(ModuleRegistry.All);

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
