using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.Modules.Platform;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary><c>.ProducesErrors(...)</c> documents an endpoint's expected failures in the OpenAPI document.</summary>
public sealed class ProducesErrorsTests
{
    [Fact]
    public async Task Each_status_is_a_problem_response_naming_its_codes()
    {
        var operation = await DocumentedOperationAsync(endpoint => endpoint.ProducesErrors(
            PlatformErrors.ConcurrencyConflict, CommonErrors.UnsupportedMediaType, CommonErrors.MalformedRequest));

        var responses = operation["responses"]!.AsObject();
        responses.Select(response => response.Key).ShouldBe(["200", "400", "409", "415"], ignoreOrder: true);

        var conflict = responses["409"]!;
        conflict["description"]!.GetValue<string>().ShouldContain($"`{PlatformErrors.ConcurrencyConflict.Code}`");
        conflict["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>()).ShouldBe([PlatformErrors.ConcurrencyConflict.Code]);
        conflict["content"]![ErrorResponseExtensions.ProblemContentType]!["schema"]!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/components/schemas/{ApiDocumentation.ProblemDetailsSchema}");
    }

    [Fact]
    public async Task Codes_sharing_a_status_are_listed_together_in_order()
    {
        var fixtureNotFound = new Error("FIXTURE_NOT_FOUND", "The fixture does not exist.", ErrorKind.NotFound);
        var contestNotFound = new Error("CONTEST_NOT_FOUND", "The contest does not exist.", ErrorKind.NotFound);

        var operation = await DocumentedOperationAsync(endpoint => endpoint.ProducesErrors(fixtureNotFound, contestNotFound));

        var notFound = operation["responses"]!["404"]!;
        notFound["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>()).ShouldBe(["CONTEST_NOT_FOUND", "FIXTURE_NOT_FOUND"]);
        notFound["description"]!.GetValue<string>().ShouldBe("Problem Details; `code`: `CONTEST_NOT_FOUND`, `FIXTURE_NOT_FOUND`");
        operation["responses"]!.AsObject().Select(response => response.Key).ShouldBe(["200", "404"], ignoreOrder: true);
    }

    [Fact]
    public async Task Declarations_on_a_group_and_its_endpoint_are_combined()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddApiDocumentation();
        await using var app = builder.Build();
        app.UseRouting();
        app.MapGroup("/fixtures").ProducesErrors(PlatformErrors.ConcurrencyConflict)
            .MapGet("/{id}", (Guid id) => TypedResults.Ok(id)).ProducesErrors(CommonErrors.MalformedRequest);
        app.UseEndpoints(static _ => { });

        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken))!;

        var responses = document["paths"]!["/fixtures/{id}"]!["get"]!["responses"]!;
        responses["409"]!["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>()).ShouldBe([PlatformErrors.ConcurrencyConflict.Code]);
        responses["400"]!["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>()).ShouldBe([CommonErrors.MalformedRequest.Code]);
    }

    // The runtime adds code and correlationId to validation problems too.
    [Fact]
    public async Task Validation_problem_schema_requires_code_and_correlation_id()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddApiDocumentation();
        await using var app = builder.Build();
        app.UseRouting();
        app.MapGet("/fixtures", () => TypedResults.Ok()).ProducesValidationProblem();
        app.UseEndpoints(static _ => { });

        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken))!;

        var schema = document["components"]!["schemas"]!["HttpValidationProblemDetails"]!;
        schema["required"]!.AsArray().Select(name => name!.GetValue<string>()).ShouldBe(["code", "correlationId"], ignoreOrder: true);
    }

    [Fact]
    public void At_least_one_error_is_required()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        Should.Throw<ArgumentException>(() => app.MapGet("/fixtures", () => TypedResults.Ok()).ProducesErrors());
    }

    private static async Task<JsonNode> DocumentedOperationAsync(Action<RouteHandlerBuilder> declare)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddApiDocumentation();

        // Built, never started, routing as SportsPlatformApp sets it up: the way the CLI exports the document.
        await using var app = builder.Build();
        app.UseRouting();
        declare(app.MapGet("/fixtures/{id}", (Guid id) => TypedResults.Ok(id)));
        app.UseEndpoints(static _ => { });

        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken))!;
        return document["paths"]!["/fixtures/{id}"]!["get"]!;
    }
}
