using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// The row version in OpenAPI (api-conventions.md, "Concurrency"): a change that takes a body <c>version</c> also takes
/// <c>If-Match</c>, a response with a <c>version</c> carries it as the <c>ETag</c>, and the document says so.
/// </summary>
public sealed class RowVersionHeadersTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    private const string _version = "Version";

    [Fact]
    public void An_endpoint_whose_body_has_a_version_declares_If_Match()
    {
        Endpoints()
            .Where(endpoint => RequestTypes(endpoint).Any(HasVersion) && endpoint.Metadata.GetMetadata<AcceptsIfMatchMetadata>() is null)
            .Select(endpoint => endpoint.DisplayName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void An_endpoint_whose_success_response_has_a_version_declares_the_ETag()
    {
        Endpoints()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Any(produces => produces.StatusCode is >= 200 and < 300 && produces.Type is { } type && HasVersion(type)))
            .Where(endpoint => endpoint.Metadata.GetMetadata<ReturnsETagMetadata>() is null)
            .Select(endpoint => endpoint.DisplayName)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task The_document_shows_If_Match_and_the_ETag()
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        var update = document["paths"]!["/api/platform/v1/tenants/{tenantId}"]!["patch"]!;
        var ifMatch = update["parameters"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == "If-Match")!;
        ifMatch["in"]!.GetValue<string>().ShouldBe("header");
        ifMatch.AsObject().ContainsKey("required").ShouldBeFalse();
        update["responses"]!["200"]!["headers"]!["ETag"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("string");
        update["responses"]!["400"]!["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>())
            .ShouldContain("VALIDATION_MALFORMED_REQUEST");

        var read = document["paths"]!["/api/platform/v1/tenants/{tenantId}"]!["get"]!;
        read["responses"]!["200"]!["headers"]!.AsObject().ContainsKey("ETag").ShouldBeTrue();
        (read["parameters"]?.AsArray() ?? []).ShouldNotContain(parameter => parameter!["name"]!.GetValue<string>() == "If-Match");
    }

    // Optional members are not `required`: the generated client lets callers leave them out.
    [Theory]
    [InlineData("ChangeTenantStatusRequest")]
    [InlineData("UpdateTenantProfileRequest")]
    [InlineData("UpdateTenantSettingsRequest")]
    [InlineData("RevokeRoleRequest")]
    public async Task A_request_without_required_members_lists_none(string schema)
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        document["components"]!["schemas"]![schema]!.AsObject().ContainsKey("required").ShouldBeFalse();
    }

    [Theory]
    [InlineData("ChangeTenantStatusWithReasonRequest", "reason")]
    [InlineData("AssignRoleRequest", "roleCode")]
    [InlineData("CreateTenantRequest", "slug,legalName,displayName,countryCode,timeZone,defaultLocale,currencyCode")]
    public async Task Only_the_members_a_request_needs_are_required(string schema, string required)
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        document["components"]!["schemas"]![schema]!["required"]!.AsArray().Select(name => name!.GetValue<string>())
            .ShouldBe(required.Split(','), ignoreOrder: true);
    }

    private IEnumerable<RouteEndpoint> Endpoints() =>
        factory.Services.GetServices<EndpointDataSource>().SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

    private static IEnumerable<Type> RequestTypes(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAcceptsMetadata>().Select(accepts => accepts.RequestType)
            .Append(endpoint.Metadata.GetMetadata<StrictJsonBodyMetadata>()?.RequestType)
            .OfType<Type>();

    private static bool HasVersion(Type type) => type.GetProperty(_version)?.PropertyType is { } property
        && (property == typeof(uint) || property == typeof(uint?));
}
