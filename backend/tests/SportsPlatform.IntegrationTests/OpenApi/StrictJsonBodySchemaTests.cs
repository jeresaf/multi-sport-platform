using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// A strict JSON body (<c>.AcceptsStrictJson&lt;T&gt;()</c>) is documented as a component named by <c>T</c>'s short name
/// unless one exists, so another type with that name would lend it the wrong schema (S02-T4b pre-review).
/// </summary>
public sealed class StrictJsonBodySchemaTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    [Fact]
    public async Task Every_strict_json_body_is_documented_with_its_own_members()
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;
        var operations = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(operation => operation.Value!))
            .ToDictionary(operation => operation["operationId"]!.GetValue<string>());
        var json = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        var strict = StrictEndpoints().ToList();

        strict.ShouldNotBeEmpty();
        foreach (var (name, requestType) in strict)
        {
            var reference = operations[name]["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
            var schema = document["components"]!["schemas"]![reference.Split('/')[^1]]!;
            schema["properties"]!.AsObject().Select(property => property.Key)
                .ShouldBe(json.GetTypeInfo(requestType).Properties.Select(property => property.Name), ignoreOrder: true, name);
        }
    }

    [Fact]
    public void Strict_json_bodies_have_distinct_type_names()
    {
        StrictEndpoints().Select(endpoint => endpoint.RequestType).Distinct()
            .GroupBy(type => type.Name)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(type => type.FullName)))
            .ShouldBeEmpty();
    }

    private IEnumerable<(string Name, Type RequestType)> StrictEndpoints() =>
        factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.Metadata.GetMetadata<StrictJsonBodyMetadata>() is not null)
            .Select(endpoint => (
                endpoint.Metadata.GetRequiredMetadata<IEndpointNameMetadata>().EndpointName,
                endpoint.Metadata.GetRequiredMetadata<StrictJsonBodyMetadata>().RequestType));
}
