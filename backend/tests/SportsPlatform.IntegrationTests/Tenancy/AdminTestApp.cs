using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The Host's services and pipeline (<see cref="SportsPlatformApp"/>) on the test database, with a test-only admin
/// endpoint: the Host has no admin endpoint until S02-T4. It answers with the request's tenant, and the evaluator
/// allows everything, so only tenant resolution and access mode refuse.
/// </summary>
internal sealed class AdminTestApp : IAsyncDisposable
{
    /// <summary>A tenant action with audit <c>REQUIRED</c>.</summary>
    public static readonly Uri FixturesPath = new("/api/admin/v1/fixtures", UriKind.Relative);

    private readonly WebApplication _app;

    private AdminTestApp(WebApplication app) => _app = app;

    public static async Task<AdminTestApp> StartAsync(PostgresFixture fixture, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(fixture.AppSettings);
        SportsPlatformApp.AddServices(builder);
        builder.Services.AddTestAuthentication();
        var authorization = new TestAuthorizationEvaluator();
        authorization.AllowAll();
        builder.Services.RemoveAll<IAuthorizationEvaluator>();
        builder.Services.AddSingleton<IAuthorizationEvaluator>(authorization);

        var app = builder.Build();
        var testApp = new AdminTestApp(app);
        SportsPlatformApp.UsePipeline(app);
        app.MapPost(FixturesPath.OriginalString, (ITenantContext tenant) => TypedResults.Ok(tenant.TenantId))
            .RequireAction(AuthorizationTestActions.FixtureCreate);
        app.UseEndpoints(static _ => { });
        await app.StartAsync(cancellationToken);
        return testApp;
    }

    public HttpClient CreateClient()
    {
        var client = _app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        TestAuthentication.ConfigureTestClient(client);
        return client;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
