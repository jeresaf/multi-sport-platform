using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The Host without a database, for tests that never touch one. Like every factory here it runs in the
/// <c>Testing</c> environment: WebApplicationFactory defaults to Development, which would load the developer
/// connection strings and migrate the local database on startup.
/// </summary>
public sealed class HostOnlyApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(TestEnvironment.Name);
}

public static class TestEnvironment
{
    public const string Name = "Testing";
}
