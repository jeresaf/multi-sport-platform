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
using SportsPlatform.Modules.Platform.Contracts.Media;

namespace SportsPlatform.IntegrationTests.OpenApi;

/// <summary>
/// Platform's media refusals in the OpenAPI document (S06-T5b): an endpoint that stores an upload
/// (<see cref="StoresMediaMetadata"/>) documents them, though its module can't name Platform's errors; others don't. Only a
/// purpose with a minimum size documents <c>MEDIA_IMAGE_TOO_SMALL</c> (S07-T4b, review #133 R1).
/// </summary>
public sealed class MediaErrorDocumentationTests
{
    [Fact]
    public async Task An_uploading_endpoint_documents_the_media_refusals()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddApiConventions(builder.Configuration);
        builder.Services.AddApiDocumentation();
        builder.Services.AddSingleton<IEntitlementCatalogue>(new DeclaredEntitlements(Array.Empty<EntitlementDefinition>()));

        // Built, never started, routing as SportsPlatformApp sets it up: the way the CLI exports the document.
        await using var app = builder.Build();
        app.UseRouting();
        app.MapModules([new BadgesModule()]);
        app.UseEndpoints(static _ => { });

        var document = JsonNode.Parse(await ApiDocumentation.ExportAsync(app.Services, TestContext.Current.CancellationToken))!;

        var upload = document["paths"]!["/api/admin/v1/badges/image"]!["put"]!["responses"]!;
        Codes(upload, "422").ShouldBe(["MEDIA_IMAGE_UNREADABLE", "MEDIA_TYPE_NOT_ALLOWED"]);
        Codes(upload, "413").ShouldBe(["MEDIA_TOO_LARGE"]);
        var rename = document["paths"]!["/api/admin/v1/badges/name"]!["put"]!["responses"]!.AsObject();
        rename.Select(response => response.Key).ShouldNotContain("422");
        rename.Select(response => response.Key).ShouldNotContain("413");
        var logo = document["paths"]!["/api/admin/v1/badges/logo"]!["put"]!["responses"]!;
        Codes(logo, "422").ShouldBe(["MEDIA_IMAGE_TOO_SMALL", "MEDIA_IMAGE_UNREADABLE", "MEDIA_TYPE_NOT_ALLOWED"]);
    }

    private static List<string> Codes(JsonNode responses, string status) =>
        [.. responses[status]!["x-error-codes"]!.AsArray().Select(code => code!.GetValue<string>())];

    private sealed class BadgesModule : IModule
    {
        public string Name => "Badges";

        public string Schema => "badges";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(ApiEndpointGroups groups)
        {
            groups.Admin.MapPut("/badges/image", () => TypedResults.Ok()).RequireAction(Action("badge.image.set"))
                .WithMetadata(new StoresMediaMetadata(MediaPurpose.PersonPhoto));
            groups.Admin.MapPut("/badges/logo", () => TypedResults.Ok()).RequireAction(Action("badge.logo.set"))
                .WithMetadata(new StoresMediaMetadata(MediaPurpose.TeamBadge));
            groups.Admin.MapPut("/badges/name", () => TypedResults.Ok()).RequireAction(Action("badge.rename"));
        }

        private static ActionDefinition Action(string code) => new(
            code, "A badge action.", ActionScope.Tenant, AuditRequirement.None, "S06", [AssignmentScope.Tenant]);
    }
}
