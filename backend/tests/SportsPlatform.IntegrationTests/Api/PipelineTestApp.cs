using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Api;
using SportsPlatform.Modules.Platform;

namespace SportsPlatform.IntegrationTests.Api;

/// <summary>
/// A minimal app with the Host's error pipeline and endpoints that fail on purpose; the real Host has no endpoints
/// that can fail yet.
/// </summary>
public sealed class PipelineTestApp : IAsyncLifetime
{
    public const string ExceptionSecret = "connection to db-internal-7 failed";

    private WebApplication? _app;

    public HttpClient CreateClient() => (_app ?? throw new InvalidOperationException("Not started.")).GetTestClient();

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApiConventions.TypeBaseUriSetting] = "https://errors.sportsplatform.invalid/",
        });
        builder.Services.AddApiConventions(builder.Configuration);

        var app = builder.Build();
        app.UseApiConventions();
        app.MapGet("/throws", IResult () => throw new InvalidOperationException(ExceptionSecret));
        app.MapGet("/conflict", () => Result.Failure(PlatformErrors.ConcurrencyConflict).ToHttpResult());
        app.MapPost("/fixtures", (FixtureRequest request) => TypedResults.Ok(request));

        await app.StartAsync(TestContext.Current.CancellationToken);
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    public sealed record FixtureRequest(string HomeTeam, DateTimeOffset KickOff);
}
