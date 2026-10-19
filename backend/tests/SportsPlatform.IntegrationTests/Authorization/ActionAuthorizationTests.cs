using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host.Actors;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Authorization;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>
/// The authorization middleware in the Host's pipeline order, with test endpoints: the real Host has no endpoint that
/// requires an action until T5.
/// </summary>
public sealed class ActionAuthorizationTests
{
    private static readonly Uri _fixtures = new("/api/admin/v1/fixtures", UriKind.Relative);

    [Fact]
    public async Task Default_evaluator_denies_with_AUTH_FORBIDDEN()
    {
        await using var app = await StartAsync(evaluator: null);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(_fixtures, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task Allowed_action_reaches_the_endpoint()
    {
        var evaluator = new TestAuthorizationEvaluator();
        evaluator.Allow(AuthorizationTestActions.FixtureRead);
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(_fixtures, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = evaluator.Requests.ShouldHaveSingleItem();
        request.Action.ShouldBeSameAs(AuthorizationTestActions.FixtureRead);
        request.Actor.ActorId.ShouldBe(SystemActor.Id);
    }

    [Fact]
    public async Task Action_the_evaluator_does_not_allow_is_denied()
    {
        var evaluator = new TestAuthorizationEvaluator();
        evaluator.Allow(AuthorizationTestActions.FixtureRead);
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(_fixtures, new FixtureRequest("Ntinda Swifts FC", new DateTimeOffset(2026, 11, 7, 13, 0, 0, TimeSpan.Zero)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        evaluator.Requests.ShouldHaveSingleItem().Action.ShouldBeSameAs(AuthorizationTestActions.FixtureCreate);
    }

    // Authorization runs before binding: a caller who is not allowed learns nothing about the request shape.
    [Fact]
    public async Task Denied_caller_with_a_malformed_body_gets_403_not_400()
    {
        await using var app = await StartAsync(new TestAuthorizationEvaluator());
        using var client = app.GetTestClient();
        using var body = new StringContent("{\"homeTeam\": ", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(_fixtures, body, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task Allowed_caller_with_a_malformed_body_gets_400()
    {
        var evaluator = new TestAuthorizationEvaluator();
        evaluator.AllowAll();
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();
        using var body = new StringContent("{\"homeTeam\": ", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(_fixtures, body, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    // The startup check makes these unreachable in the Host; the middleware still refuses them.
    [Theory]
    [InlineData("/api/admin/v1/unmarked")]
    [InlineData("/api/admin/v1/both")]
    public async Task Endpoint_without_exactly_one_action_is_refused_at_runtime(string path)
    {
        var evaluator = new TestAuthorizationEvaluator();
        evaluator.AllowAll();
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        evaluator.Requests.ShouldBeEmpty();
    }

    // An endpoint set by other middleware is not a RouteEndpoint; its declared action is still enforced.
    [Fact]
    public async Task Marked_endpoint_that_is_not_a_route_endpoint_is_evaluated()
    {
        var evaluator = new TestAuthorizationEvaluator();
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();

        using var denied = await client.GetAsync(new Uri(_customEndpointPath, UriKind.Relative), Ct);
        evaluator.Allow(AuthorizationTestActions.FixtureRead);
        using var allowed = await client.GetAsync(new Uri(_customEndpointPath, UriKind.Relative), Ct);

        await denied.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        evaluator.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Public_endpoint_is_not_evaluated()
    {
        var evaluator = new TestAuthorizationEvaluator();
        await using var app = await StartAsync(evaluator);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/api/public/v1/reference/countries", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        evaluator.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unmatched_route_and_method_keep_their_codes()
    {
        await using var app = await StartAsync(evaluator: null);
        using var client = app.GetTestClient();

        using var notFound = await client.GetAsync(new Uri("/api/admin/v1/nothing-here", UriKind.Relative), Ct);
        using var wrongMethod = await client.DeleteAsync(_fixtures, Ct);

        await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_ROUTE_NOT_FOUND");
        await wrongMethod.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "PLATFORM_METHOD_NOT_ALLOWED");
    }

    private const string _customEndpointPath = "/custom-endpoint";

    private static readonly Guid _tenantId = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The Host's error pipeline and authorization, in <c>SportsPlatformApp</c>'s order.</summary>
    private static async Task<WebApplication> StartAsync(TestAuthorizationEvaluator? evaluator)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApiConventions.TypeBaseUriSetting] = "https://errors.sportsplatform.invalid/",
        });
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddCurrentActor();
        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        builder.Services.AddSingleton<ITenantAccess>(new FullAccess());
        if (evaluator is not null)
        {
            builder.Services.AddSingleton<IAuthorizationEvaluator>(evaluator);
        }

        builder.Services.AddActionAuthorization();

        var app = builder.Build();
        app.UseApiConventions();
        app.UseRouting();

        // Stands in for tenant resolution: the fixture actions are tenant actions (TenantResolutionTests covers it).
        app.Use((context, next) =>
        {
            context.RequestServices.GetRequiredService<TenantContext>().Set(_tenantId);
            return next(context);
        });
        app.Use((context, next) =>
        {
            if (context.Request.Path == _customEndpointPath)
            {
                context.SetEndpoint(new Endpoint(
                    static endpointContext => endpointContext.Response.WriteAsync("reached", endpointContext.RequestAborted),
                    new EndpointMetadataCollection(new RequiresActionMetadata(AuthorizationTestActions.FixtureRead)),
                    "Custom endpoint"));
            }

            return next(context);
        });
        app.UseActionAuthorization();

        app.MapGet("/api/admin/v1/fixtures", () => TypedResults.Ok()).RequireAction(AuthorizationTestActions.FixtureRead);
        app.MapPost("/api/admin/v1/fixtures", (FixtureRequest request) => TypedResults.Ok(request)).RequireAction(AuthorizationTestActions.FixtureCreate);
        app.MapGet("/api/admin/v1/unmarked", () => TypedResults.Ok());
        app.MapGet("/api/admin/v1/both", () => TypedResults.Ok()).AllowPublic().RequireAction(AuthorizationTestActions.FixtureRead);
        app.MapGet("/api/public/v1/reference/countries", () => TypedResults.Ok()).AllowPublic();
        app.UseEndpoints(static _ => { });

        await app.StartAsync(Ct);
        return app;
    }

    public sealed record FixtureRequest(string HomeTeam, DateTimeOffset KickOff);

    private sealed class FullAccess : ITenantAccess
    {
        public Task<TenantAccessMode?> GetAccessModeAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantAccessMode?>(TenantAccessMode.Full);

        public Task<Result> CheckAdminAccessAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }
}
