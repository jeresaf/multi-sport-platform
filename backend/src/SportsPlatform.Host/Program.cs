using SportsPlatform.Host;

var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(args));

await StartupMigration.RunIfEnabledAsync(app);

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
