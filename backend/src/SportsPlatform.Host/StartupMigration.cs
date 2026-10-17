using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.Host;

/// <summary>
/// Development convenience: migrate when the API starts. Every other environment migrates with the CLI
/// <c>migrate</c> command (ADR-0016), so the API process never holds the <c>sp_migrator</c> credentials there.
/// </summary>
public static class StartupMigration
{
    public const string SettingKey = "Database:MigrateOnStartup";

    public static async Task RunIfEnabledAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Configuration.GetValue<bool>(SettingKey))
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{SettingKey} is allowed only in Development (environment: {app.Environment.EnvironmentName}). " +
                "Apply migrations with the CLI: dotnet run --project backend/tools/SportsPlatform.Cli -- migrate");
        }

        await app.Services.GetRequiredService<MigrationRunner>().RunAsync();
    }
}
