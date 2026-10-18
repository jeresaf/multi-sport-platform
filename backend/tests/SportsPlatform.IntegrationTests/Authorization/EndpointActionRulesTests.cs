using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>Fail closed at startup: the rules the Host checks when it is built (S01-T4).</summary>
public sealed class EndpointActionRulesTests
{
    private static readonly IReadOnlyList<ActionCatalogueEntry> _catalogue =
    [
        new(AuthorizationTestActions.FixtureRead, "Fixtures"),
        new(AuthorizationTestActions.FixtureCreate, "Fixtures"),
    ];

    // S01-T4 acceptance: an endpoint without an action fails the startup check.
    [Fact]
    public async Task Endpoint_without_an_action_fails_the_startup_check()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/api/admin/v1/fixtures", () => Results.Ok());

        var exception = Should.Throw<InvalidOperationException>(() => app.EnsureEndpointsDeclareActions(_catalogue));

        exception.Message.ShouldContain("GET /api/admin/v1/fixtures declares neither .RequireAction(...) nor .AllowPublic().");
    }

    // S01-T4 acceptance: so does an endpoint with both markers.
    [Fact]
    public async Task Endpoint_with_both_markers_fails_the_startup_check()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/api/public/v1/fixtures", () => Results.Ok()).AllowPublic().RequireAction(AuthorizationTestActions.FixtureRead);

        var exception = Should.Throw<InvalidOperationException>(() => app.EnsureEndpointsDeclareActions(_catalogue));

        exception.Message.ShouldContain("GET /api/public/v1/fixtures declares both .RequireAction(...) and .AllowPublic().");
    }

    [Fact]
    public async Task Correctly_marked_endpoints_pass()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/health/live", () => Results.Ok()).AllowPublic();
        app.MapGroup("/api/admin/v1/fixtures").RequireAction(AuthorizationTestActions.FixtureRead).MapGet("/{id}", (Guid id) => Results.Ok(id));
        app.MapPost("/api/admin/v1/fixtures", () => Results.Ok()).RequireAction(AuthorizationTestActions.FixtureCreate);

        Validate(app).ShouldBeEmpty();
        Should.NotThrow(() => app.EnsureEndpointsDeclareActions(_catalogue));
    }

    [Fact]
    public async Task Endpoint_with_two_actions_is_rejected()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGroup("/api/admin/v1").RequireAction(AuthorizationTestActions.FixtureRead)
            .MapPost("/fixtures", () => Results.Ok()).RequireAction(AuthorizationTestActions.FixtureCreate);

        Validate(app).ShouldHaveSingleItem().ShouldBe("POST /api/admin/v1/fixtures declares more than one action (fixture.read, fixture.create).");
    }

    [Theory]
    [InlineData("/api/admin/v1/fixtures")]
    [InlineData("/api/platform/v1/diagnostics/outbox")]
    [InlineData("api/Admin")]
    [InlineData("~/api/admin/v1/fixtures")]
    public async Task Admin_and_platform_endpoints_cannot_be_public(string path)
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet(path, () => Results.Ok()).AllowPublic();

        Validate(app).ShouldHaveSingleItem().ShouldContain("cannot be .AllowPublic()");
    }

    [Fact]
    public async Task Paths_that_only_start_like_a_protected_family_may_be_public()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/api/administrators", () => Results.Ok()).AllowPublic();

        Validate(app).ShouldBeEmpty();
    }

    [Fact]
    public async Task Action_missing_from_the_catalogue_is_rejected()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapPost("/api/admin/v1/fixtures", () => Results.Ok()).RequireAction(AuthorizationTestActions.FixtureCreate);

        EndpointActionRules.Validate(Endpoints(app), [new(AuthorizationTestActions.FixtureRead, "Fixtures")])
            .ShouldHaveSingleItem().ShouldContain("requires 'fixture.create', which is not declared");
    }

    // The Host marks the public group, so a module's public endpoint that also requires an action has both markers.
    [Fact]
    public async Task Module_endpoints_are_public_only_in_the_public_family()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapModules([new ProbeModule()]);

        Validate(app).ShouldBe(
        [
            "GET /api/public/v1/fixtures/{id} declares both .RequireAction(...) and .AllowPublic().",
            "GET /api/platform/v1/probes declares neither .RequireAction(...) nor .AllowPublic().",
        ], ignoreOrder: true);
    }

    // S02-T3a: the Host sets the tenant from the route, so the endpoint must be a platform one and name the parameter.
    [Fact]
    public async Task Endpoint_running_under_the_route_tenant_needs_a_platform_action_and_the_parameter()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/api/platform/v1/tenants/{tenantId:guid}", (Guid tenantId) => Results.Ok(tenantId))
            .RequireAction(TenancyActions.TenantRead).RunsUnderRouteTenant();
        app.MapGet("/api/platform/v1/tenants", () => Results.Ok())
            .RequireAction(TenancyActions.TenantRead).RunsUnderRouteTenant();
        app.MapGet("/api/admin/v1/fixtures/{tenantId:guid}", (Guid tenantId) => Results.Ok(tenantId))
            .RequireAction(AuthorizationTestActions.FixtureRead).RunsUnderRouteTenant();

        EndpointActionRules.Validate(Endpoints(app), [.. _catalogue, new(TenancyActions.TenantRead, "Tenancy")]).ShouldBe(
        [
            "GET /api/platform/v1/tenants runs under the route's tenant, so it needs one platform action and a '{tenantId}' route parameter.",
            "GET /api/admin/v1/fixtures/{tenantId:guid} runs under the route's tenant, so it needs one platform action and a '{tenantId}' route parameter.",
        ], ignoreOrder: true);
    }

    private static IReadOnlyList<string> Validate(WebApplication app) => EndpointActionRules.Validate(Endpoints(app), _catalogue);

    private static IEnumerable<Endpoint> Endpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);

    private sealed class ProbeModule : IModule
    {
        public string Name => "Probe";

        public string Schema => "probe";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(ApiEndpointGroups groups)
        {
            groups.Public.MapGet("/reference/countries", () => Results.Ok());
            groups.Public.MapGet("/fixtures/{id}", (Guid id) => Results.Ok(id)).RequireAction(AuthorizationTestActions.FixtureRead);
            groups.Admin.MapGet("/fixtures", () => Results.Ok()).RequireAction(AuthorizationTestActions.FixtureRead);
            groups.Platform.MapGet("/probes", () => Results.Ok());
        }
    }
}
