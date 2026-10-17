using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The Host without a database, for tests that never touch one. Like every factory here it runs in the
/// <c>Testing</c> environment: WebApplicationFactory defaults to Development, which would load the developer
/// connection strings and migrate the local database on startup.
/// </summary>
public sealed class HostOnlyApp : WebApplicationFactory<Program>
{
    public TelemetryCapture Telemetry { get; } = new();

    public TestTenantContext Tenant { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestEnvironment.Name);
        builder.ConfigureTestServices(services =>
        {
            Telemetry.AddTo(services);
            services.AddSingleton<ITenantContext>(Tenant);
        });
    }
}

public static class TestEnvironment
{
    public const string Name = "Testing";
}
