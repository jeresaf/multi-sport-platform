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

    // S07-T1b-2: a list's limit is an integer with its bounds and default, so the client types it.
    [Theory]
    [InlineData("/api/admin/v1/people")]
    [InlineData("/api/admin/v1/venues")]
    public async Task A_lists_limit_is_an_integer_from_1_to_100_defaulting_to_25(string path)
    {
        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(factory.Services, TestContext.Current.CancellationToken))!;

        var parameters = document["paths"]![path]!["get"]!["parameters"]!.AsArray();
        var schema = parameters.Single(parameter => parameter!["name"]!.GetValue<string>() == "limit")!["schema"]!;

        (schema["type"]!.GetValue<string>(), schema["minimum"]!.GetValue<int>(), schema["maximum"]!.GetValue<int>(), schema["default"]!.GetValue<int>())
            .ShouldBe(("integer", 1, 100, 25));
    }
}
