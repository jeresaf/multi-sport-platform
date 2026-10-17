using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.Modules;

namespace SportsPlatform.IntegrationTests.Modules;

public sealed class EndpointGroupTests
{
    [Fact]
    public async Task Module_endpoints_are_mapped_under_their_API_family_prefix()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        var probe = new ProbeModule();

        app.MapModules([probe]);

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order(StringComparer.Ordinal)
            .ToList();

        routes.ShouldBe(
        [
            "/api/admin/v1/probes",
            "/api/platform/v1/probes",
            "/api/public/v1/probes",
        ]);
    }

    [Fact]
    public void Module_services_are_added()
    {
        var services = new ServiceCollection();

        services.AddModules(new ConfigurationBuilder().Build(), [new ProbeModule()]);

        services.ShouldContain(d => d.ServiceType == typeof(ProbeModule.Marker));
    }

    private sealed class ProbeModule : IModule
    {
        public string Name => "Probe";

        public string Schema => "probe";

        public void AddServices(IServiceCollection services, IConfiguration configuration) =>
            services.AddSingleton<Marker>();

        public void MapEndpoints(ApiEndpointGroups groups)
        {
            groups.Public.MapGet("/probes", () => Results.Ok());
            groups.Admin.MapGet("/probes", () => Results.Ok());
            groups.Platform.MapGet("/probes", () => Results.Ok());
        }

        public sealed class Marker;
    }
}
