using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The Host without a database, for tests that never touch one. Like every factory here it runs in the
/// <c>Testing</c> environment: WebApplicationFactory defaults to Development, which would load the developer
/// connection strings and migrate the local database on startup. With no database there is no tenant resolution to
/// reach, so <c>X-Test-Scope-Tenant</c> sets the request's tenant directly.
/// </summary>
public sealed class HostOnlyApp : WebApplicationFactory<Program>
{
    public const string ScopeTenantHeader = "X-Test-Scope-Tenant";

    public TelemetryCapture Telemetry { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestEnvironment.Name);
        builder.ConfigureTestServices(services =>
        {
            Telemetry.AddTo(services);
            services.AddTestAuthentication();
            services.AddTransient<IStartupFilter, ScopeTenantFilter>();
        });
    }

    private sealed class ScopeTenantFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (Guid.TryParse(context.Request.Headers[ScopeTenantHeader], out var tenantId))
                {
                    context.RequestServices.GetRequiredService<TenantContext>().Set(tenantId);
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public static class TestEnvironment
{
    public const string Name = "Testing";
}
