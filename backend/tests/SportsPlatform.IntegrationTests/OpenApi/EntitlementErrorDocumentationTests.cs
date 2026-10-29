using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Host.OpenApi;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// The pipeline's step 2 refusal in the OpenAPI document (S04 D10): <c>ENTITLEMENT_REQUIRED</c> on an admin endpoint whose
/// action names a boolean or level entitlement, not on one naming a quota (its handler counts) or none.
/// </summary>
public sealed class EntitlementErrorDocumentationTests
{
    private static readonly EntitlementDefinition _publishing = new(
        "FIXTURE_PUBLISHING", "Fixture publishing", "Publish fixtures.", EntitlementType.Boolean, "Competitions", "S04");

    private static readonly EntitlementDefinition _reports = new(
        "FIXTURE_REPORTS", "Fixture reports", "Fixture reports.", EntitlementType.Level, "Competitions", "S04", levels: ["BASIC", "FULL"]);

    private static readonly EntitlementDefinition _slots = new(
        "FIXTURE_SLOTS", "Fixture slots", "Fixtures per edition.", EntitlementType.Quota, "Competitions", "S04", unit: "fixtures");

    [Theory]
    [InlineData("/api/admin/v1/fixtures/publish", true)]
    [InlineData("/api/admin/v1/fixtures/report", true)]
    [InlineData("/api/admin/v1/fixtures", false)]
    [InlineData("/api/admin/v1/fixtures/unnamed", false)]
    public async Task Entitlement_required_is_documented_where_step_2_can_refuse(string path, bool documented)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddApiDocumentation();
        builder.Services.AddSingleton<IEntitlementCatalogue>(new DeclaredEntitlements([_publishing, _reports, _slots]));

        // Built, never started, routing as SportsPlatformApp sets it up: the way the CLI exports the document.
        await using var app = builder.Build();
        app.UseRouting();
        app.MapModules([new FixturesModule()]);
        app.UseEndpoints(static _ => { });

        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken))!;

        var codes = document["paths"]![path]!["post"]!["responses"]!["403"]!["x-error-codes"]!.AsArray()
            .Select(code => code!.GetValue<string>()).ToList();
        codes.Contains("ENTITLEMENT_REQUIRED").ShouldBe(documented);
        codes.ShouldContain("AUTH_FORBIDDEN");
    }

    private sealed class FixturesModule : IModule
    {
        public string Name => "Fixtures";

        public string Schema => "fixtures";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(ApiEndpointGroups groups)
        {
            groups.Admin.MapPost("/fixtures/publish", () => TypedResults.Ok()).RequireAction(Action("fixture.publish", _publishing));
            groups.Admin.MapPost("/fixtures/report", () => TypedResults.Ok()).RequireAction(Action("fixture.report", _reports));
            groups.Admin.MapPost("/fixtures", () => TypedResults.Ok()).RequireAction(Action("fixture.create", _slots));
            groups.Admin.MapPost("/fixtures/unnamed", () => TypedResults.Ok()).RequireAction(Action("fixture.rename", entitlement: null));
        }

        private static ActionDefinition Action(string code, EntitlementDefinition? entitlement) => new(
            code, "A fixture action.", ActionScope.Tenant, AuditRequirement.None, "S04", [AssignmentScope.Tenant], entitlement?.Code);
    }
}
