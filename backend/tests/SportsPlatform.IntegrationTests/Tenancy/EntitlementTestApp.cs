using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The Host's services, pipeline and module endpoints on the test database, plus S04 D20's test-only boolean
/// entitlement <see cref="FixturePublishing"/> and a test admin endpoint whose action names it (no real action is
/// PRO-only yet). The entitlement catalogue is built explicitly from the declared entitlements and the test one (#63
/// R9), never by scanning the test assembly. The plans' values of the test entitlement are rows a test writes as the
/// owner (<see cref="SavePlanValuesSql"/>) and removes (<see cref="RemovePlanValuesSql"/>); the app starts after
/// they are written, so its plan values (cached until restart, D7) include them. The clock is fake; the evaluator
/// (step 3) denies everything until a test allows an action.
/// </summary>
internal sealed class EntitlementTestApp : IAsyncDisposable
{
    /// <summary>The test entitlement: off on STANDARD, on on PRO.</summary>
    public static readonly EntitlementDefinition FixturePublishing = new(
        "FIXTURE_PUBLISHING", "Fixture publishing", "Publish a competition's fixtures to the public site.",
        EntitlementType.Boolean, enforcedBy: "Competitions", since: "S04");

    /// <summary>A tenant write that names <see cref="FixturePublishing"/>; audit <c>REQUIRED</c>.</summary>
    public static readonly ActionDefinition FixturePublish = new(
        "fixture.publish", "Publish fixtures.", ActionScope.Tenant, AuditRequirement.Required, "S04",
        [AssignmentScope.Tenant], entitlement: FixturePublishing.Code);

    public static readonly Uri PublishPath = new("/api/admin/v1/fixtures/publish", UriKind.Relative);

    public static readonly string SavePlanValuesSql =
        "INSERT INTO tenancy.plan_entitlements (id, plan_id, entitlement_code, value, created_at, created_by, updated_at, updated_by) " +
        "SELECT gen_random_uuid(), p.id, 'FIXTURE_PUBLISHING', " +
        "CASE p.code WHEN 'PRO' THEN '{\"enabled\":true}'::jsonb ELSE '{\"enabled\":false}'::jsonb END, " +
        "now(), '00000000-0000-0000-0000-000000000000', now(), '00000000-0000-0000-0000-000000000000' FROM tenancy.plans p";

    public static readonly string RemovePlanValuesSql = "DELETE FROM tenancy.plan_entitlements WHERE entitlement_code = 'FIXTURE_PUBLISHING'";

    private readonly WebApplication _app;

    private EntitlementTestApp(WebApplication app, FakeTimeProvider clock, TestAuthorizationEvaluator authorization)
    {
        _app = app;
        Clock = clock;
        Authorization = authorization;
    }

    public FakeTimeProvider Clock { get; }

    public TestAuthorizationEvaluator Authorization { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<EntitlementTestApp> StartAsync(PostgresFixture fixture, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(fixture.AppSettings);
        SportsPlatformApp.AddServices(builder);
        builder.Services.AddTestAuthentication();

        var clock = new FakeTimeProvider(now);
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton<TimeProvider>(clock);
        var authorization = new TestAuthorizationEvaluator();
        builder.Services.RemoveAll<IAuthorizationEvaluator>();
        builder.Services.AddSingleton<IAuthorizationEvaluator>(authorization);
        builder.Services.RemoveAll<IEntitlementCatalogue>();
        builder.Services.AddSingleton<IEntitlementCatalogue>(
            new DeclaredEntitlements([.. new DeclaredEntitlements(ModuleRegistry.All).All, FixturePublishing]));

        var app = builder.Build();
        SportsPlatformApp.UsePipeline(app);
        app.MapModules(ModuleRegistry.All);
        app.MapPost(PublishPath.OriginalString, (ITenantContext tenant) => TypedResults.Ok(tenant.TenantId))
            .RequireAction(FixturePublish);
        app.UseEndpoints(static _ => { });
        await app.StartAsync(cancellationToken);
        return new EntitlementTestApp(app, clock, authorization);
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
