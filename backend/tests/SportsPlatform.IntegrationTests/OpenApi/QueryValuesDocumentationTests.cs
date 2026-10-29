using System.Text.Json.Nodes;
using SportsPlatform.Host.OpenApi;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// A query parameter bound as text that declares its values (<c>DocumentsQueryValues</c>) shows them as its
/// <c>enum</c>, so the generated client types it (S03 follow-up, S04 D14). The validator still refuses a bad value.
/// </summary>
public sealed class QueryValuesDocumentationTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    [Fact]
    public async Task The_capabilities_scopeType_lists_the_resource_types()
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        var parameters = document["paths"]!["/api/admin/v1/me/capabilities"]!["get"]!["parameters"]!.AsArray();
        var scopeType = parameters.Single(parameter => parameter!["name"]!.GetValue<string>() == "scopeType")!;
        scopeType["in"]!.GetValue<string>().ShouldBe("query");
        scopeType["schema"]!["type"]!.GetValue<string>().ShouldBe("string");
        scopeType["schema"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe(["COMPETITION", "TEAM", "CONTEST"]);

        var scopeId = parameters.Single(parameter => parameter!["name"]!.GetValue<string>() == "scopeId")!;
        scopeId["schema"]!.AsObject().ContainsKey("enum").ShouldBeFalse();
    }
}
