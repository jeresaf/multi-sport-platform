using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>The real Host's endpoints, in both environments that map different ones (Development adds OpenAPI and Scalar).</summary>
public sealed class HostEndpointActionsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Every_Host_endpoint_declares_exactly_one_marker(string environment)
    {
        // Built, never started: no server, no startup migration. Build itself throws on a violation.
        await using var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment }));

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList();

        endpoints.ShouldNotBeEmpty();
        EndpointActionRules.Validate(endpoints, ActionCatalogue.Build(ModuleRegistry.All)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Testing", new[] { "/health/live", "/health/ready" })]
    [InlineData("Development", new[] { "/health/live", "/health/ready", "/openapi/{documentName}.json", "/scalar/{documentName?}", "/scalar/scalar.js" })]
    public async Task Probes_and_documentation_are_public(string environment, string[] expected)
    {
        await using var app = SportsPlatformApp.Build(WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment }));

        var publicPaths = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowPublicMetadata>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        foreach (var path in expected)
        {
            publicPaths.ShouldContain(path);
        }

        if (environment == TestEnvironment.Name)
        {
            publicPaths.ShouldNotContain(path => path!.StartsWith("/scalar", StringComparison.Ordinal));
        }
    }
}

/// <summary>The real Host's authorization registration; OutboxDiagnosticsTests checks allowed and denied end to end.</summary>
public sealed class HostAuthorizationTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    [Fact]
    public void Host_denies_by_default()
    {
        factory.Services.GetRequiredService<IAuthorizationEvaluator>().ShouldBeOfType<DenyAllAuthorizationEvaluator>();
    }

    [Fact]
    public async Task Wrong_method_on_a_public_endpoint_is_405_not_403()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(new Uri("/health/live", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(System.Net.HttpStatusCode.MethodNotAllowed, "PLATFORM_METHOD_NOT_ALLOWED");
    }
}
