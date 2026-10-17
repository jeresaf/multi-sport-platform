using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Host.Api;

namespace SportsPlatform.IntegrationTests.Api;

/// <summary>
/// Limits enforced by Kestrel itself (TestServer enforces none): the error pipeline on a real server, port 0.
/// </summary>
public sealed class KestrelLimitsTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Json_body_over_the_size_limit_returns_413(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseKestrel(options => options.Limits.MaxRequestBodySize = 64).UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApiConventions.TypeBaseUriSetting] = "https://errors.sportsplatform.invalid/",
        });
        builder.Services.AddApiConventions(builder.Configuration);
        await using var app = builder.Build();
        app.UseApiConventions();
        app.MapPost("/fixtures", (PipelineTestApp.FixtureRequest request) => TypedResults.Ok(request));
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var json = $"{{\"homeTeam\":\"{new string('x', 200)}\",\"kickOff\":\"2026-10-17T15:00:00Z\"}}";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/fixtures", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "VALIDATION_REQUEST_TOO_LARGE");
    }
}
