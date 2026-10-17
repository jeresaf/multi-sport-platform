using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SportsPlatform.Host;

namespace SportsPlatform.IntegrationTests.Host;

public sealed class StartupMigrationTests
{
    // The guard fires before any connection is opened, so these hosts need no database.
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Migrating_on_startup_outside_Development_stops_the_API(string environment)
    {
        using var app = new MigrateOnStartupApp(environment);

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldContain($"{StartupMigration.SettingKey} is allowed only in Development");
    }

    private sealed class MigrateOnStartupApp(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting(StartupMigration.SettingKey, "true");
        }
    }
}
