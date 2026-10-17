using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Observability;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform;

namespace SportsPlatform.IntegrationTests.Api;

/// <summary>
/// A minimal app with the Host's logging and error pipeline and endpoints that fail on purpose; the real Host has no endpoints
/// that can fail yet. Development differs: binding failures throw (RouteHandlerOptions.ThrowOnBadRequest) and the
/// developer exception page is on.
/// </summary>
public abstract class PipelineTestApp(string environmentName) : IAsyncLifetime
{
    public const string ExceptionSecret = "connection to db-internal-7 failed";

    private WebApplication? _app;

    public TelemetryCapture Telemetry { get; } = new();

    public HttpClient CreateClient() => (_app ?? throw new InvalidOperationException("Not started.")).GetTestClient();

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApiConventions.TypeBaseUriSetting] = "https://errors.sportsplatform.invalid/",
        });
        builder.AddObservability();
        builder.Services.AddApiConventions(builder.Configuration);
        Telemetry.AddTo(builder.Services);

        var app = builder.Build();
        app.UseRequestLogging();
        app.UseApiConventions();
        app.MapGet("/throws", IResult () => throw new InvalidOperationException(ExceptionSecret));
        app.MapGet("/conflict", () => Result.Failure(PlatformErrors.ConcurrencyConflict).ToHttpResult());
        app.MapPost("/fixtures", (FixtureRequest request) => TypedResults.Ok(request));
        app.MapGet("/bare-not-found", () => TypedResults.NotFound());
        // What Kestrel throws when a body exceeds MaxRequestBodySize (TestServer enforces no limit).
        app.MapPost("/too-large", IResult () => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        await app.StartAsync(TestContext.Current.CancellationToken);
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    public sealed record FixtureRequest(string HomeTeam, DateTimeOffset KickOff);
}

public sealed class TestingPipelineApp() : PipelineTestApp("Testing");

public sealed class DevelopmentPipelineApp() : PipelineTestApp("Development");
