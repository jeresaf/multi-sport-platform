using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The whole Host (<see cref="SportsPlatformApp.Build"/>) on the test database, in a chosen environment, trusting
/// <see cref="PublicRequests.Proxy"/> as a forwarding proxy: for what <see cref="TestApp"/> can't vary (the dev override
/// is Development-only, the proxies and base domain are configuration). Public requests only: nobody is authenticated.
/// </summary>
internal sealed class PublicTestApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private PublicTestApp(WebApplication app) => _app = app;

    public TestServer Server => _app.GetTestServer();

    public IServiceProvider Services => _app.Services;

    public static async Task<PublicTestApp> StartAsync(
        PostgresFixture fixture, string environment, CancellationToken cancellationToken, params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(fixture.AppSettings);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Development's appsettings would start the outbox processor on the shared test database.
            ["Outbox:Enabled"] = "false",
            ["ForwardedHeaders:KnownProxies:0"] = PublicRequests.Proxy.ToString(),

            // Only Development and Testing name a mail server; nothing sends here (the outbox is off).
            ["Email:From"] = "no-reply@sports.test",
            ["Email:Smtp:Host"] = "smtp.sports.test",
        });
        builder.Configuration.AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value)));

        var app = SportsPlatformApp.Build(builder);
        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new PublicTestApp(app);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
