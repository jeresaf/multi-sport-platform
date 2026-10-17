using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SportsPlatform.Host;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>The OpenAPI document the CLI exports to <c>contracts/openapi.json</c>.</summary>
public sealed class ApiDocumentationTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    [Fact]
    public async Task Document_describes_the_error_contract()
    {
        var document = await ExportAsync();

        document["openapi"]!.GetValue<string>().ShouldStartWith("3.1");
        document["info"]!["title"]!.GetValue<string>().ShouldBe("MSP API");
        document["info"]!["version"]!.GetValue<string>().ShouldBe(ApiDocumentation.DocumentName);

        var problem = document["components"]!["schemas"]![ApiDocumentation.ProblemDetailsSchema]!;
        problem["properties"]!["code"]!["type"]!.GetValue<string>().ShouldBe("string");
        problem["properties"]!["correlationId"]!["type"]!.GetValue<string>().ShouldBe("string");
        problem["required"]!.AsArray().Select(name => name!.GetValue<string>()).ShouldBe(["code", "correlationId"], ignoreOrder: true);
    }

    // The web default reads numbers from strings, which documents every number as `integer | string`.
    [Fact]
    public async Task Numbers_are_documented_as_numbers_only()
    {
        var document = await ExportAsync();

        var status = document["components"]!["schemas"]![ApiDocumentation.ProblemDetailsSchema]!["properties"]!["status"]!;
        status["type"]!.AsArray().Select(type => type!.GetValue<string>()).ShouldBe(["null", "integer"], ignoreOrder: true);
    }

    [Fact]
    public async Task Document_does_not_depend_on_the_request_or_the_run()
    {
        using var client = factory.CreateClient();
        using var _ = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        var first = await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken);
        var second = await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken);

        second.ShouldBe(first);
        first.ShouldEndWith("}\n");
        first.ShouldNotContain("\r");
        JsonNode.Parse(first)!.AsObject().ContainsKey("servers").ShouldBeFalse();
    }

    // The CLI builds the app without starting it; endpoints must be in the document all the same.
    [Fact]
    public async Task Export_from_an_app_that_never_started_matches_the_running_app()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = TestEnvironment.Name });
        await using var app = SportsPlatformApp.Build(builder);

        var exported = await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken);

        exported.ShouldBe(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Health_probes_are_not_part_of_the_document()
    {
        var document = await ExportAsync();

        document["paths"]!.AsObject().Select(path => path.Key).ShouldNotContain(path => path.StartsWith("/health", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Document_and_Scalar_are_not_served_outside_Development(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_ROUTE_NOT_FOUND");
    }

    private async Task<JsonNode> ExportAsync() =>
        JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;
}

public sealed class DevelopmentApiDocumentationTests(DevelopmentHostApp factory) : IClassFixture<DevelopmentHostApp>
{
    [Fact]
    public async Task Development_serves_the_document()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        document["info"]!["title"]!.GetValue<string>().ShouldBe("MSP API");
        document.AsObject().ContainsKey("servers").ShouldBeFalse();
    }

    // CI exports in Development (the CLI's launch profile): the documentation endpoints must not document themselves.
    [Fact]
    public async Task Development_document_does_not_list_the_documentation_endpoints()
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        document["paths"]!.AsObject().Select(path => path.Key)
            .ShouldNotContain(path => path.StartsWith("/openapi", StringComparison.Ordinal) || path.StartsWith("/scalar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_serves_Scalar()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
    }
}

/// <summary>
/// The Host in Development, for Development-only endpoints. Startup migration is switched off and nothing here
/// touches the database, so the developer connection strings are never used.
/// </summary>
public sealed class DevelopmentHostApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [StartupMigration.SettingKey] = "false",
        }));
    }
}
