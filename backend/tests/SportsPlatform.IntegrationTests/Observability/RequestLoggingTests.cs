using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using Serilog.Events;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Observability;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Observability;

/// <summary>The real Host's logging and tracing (no database).</summary>
public sealed class RequestLoggingTests(HostOnlyApp app) : IClassFixture<HostOnlyApp>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_is_logged_with_correlation_ID_and_actor()
    {
        using var response = await GetAsync("/fixtures-board", correlationId: "kmsa-req-0001");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var logEvent = await app.Telemetry.WaitForRequestLogAsync("/fixtures-board");
        logEvent.Level.ShouldBe(LogEventLevel.Information);
        Scalar(logEvent, RequestContextEnricher.CorrelationId).ShouldBe("kmsa-req-0001");
        // A request from a caller who is not signed in is the anonymous actor's (S03 D3; S01 #17 R1).
        Scalar(logEvent, RequestContextEnricher.ActorKind).ShouldBe(nameof(ActorKind.Anonymous));
        Scalar(logEvent, RequestContextEnricher.ActorId).ShouldBe(AnonymousActor.Id);
        Scalar(logEvent, "StatusCode").ShouldBe(404);
        logEvent.Properties.ShouldNotContainKey(RequestContextEnricher.TenantId);
    }

    [Fact]
    public async Task Request_with_a_tenant_is_logged_with_the_tenant()
    {
        var tenantId = Guid.CreateVersion7();
        using var response = await GetAsync("/standings-board", headers: (HostOnlyApp.ScopeTenantHeader, tenantId.ToString()));

        var logEvent = await app.Telemetry.WaitForRequestLogAsync("/standings-board");
        Scalar(logEvent, RequestContextEnricher.TenantId).ShouldBe(tenantId);
        Scalar(logEvent, RequestContextEnricher.CorrelationId).ShouldBe(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Request_is_traced_without_query_values_and_logged_without_the_query()
    {
        using var response = await GetAsync("/people-search?email=nakato.namubiru@example.org");

        var span = await app.Telemetry.WaitForServerSpanAsync("/people-search");
        var logEvent = await app.Telemetry.WaitForRequestLogAsync("/people-search");
        span.TagObjects.ShouldAllBe(tag => !(tag.Value ?? "").ToString()!.Contains("nakato", StringComparison.Ordinal));
        logEvent.RenderMessage(null).ShouldNotContain("nakato");
        logEvent.Properties.Values.ShouldAllBe(value => !value.ToString().Contains("nakato", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/health/live", false)]
    [InlineData("/health/ready", false)]
    [InlineData("/health/other", true)]
    [InlineData("/api/public/v1/fixtures", true)]
    public void Only_non_health_requests_are_traced(string path, bool traced)
    {
        var options = app.Services.GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>().Get(Options.DefaultName);

        options.Filter.ShouldNotBeNull()(Request(path, StatusCodes.Status200OK)).ShouldBe(traced);
    }

    [Theory]
    [InlineData("/health/live", 200, LogEventLevel.Verbose)]
    [InlineData("/health/ready", 503, LogEventLevel.Error)]
    [InlineData("/api/public/v1/fixtures", 404, LogEventLevel.Information)]
    [InlineData("/api/public/v1/fixtures", 500, LogEventLevel.Error)]
    public void Health_probes_are_logged_below_information_unless_they_fail(string path, int status, LogEventLevel level) =>
        ObservabilitySetup.RequestLogLevel(Request(path, status), exception: null).ShouldBe(level);

    [Fact]
    public async Task Authenticated_request_is_logged_with_its_user()
    {
        var userId = Guid.CreateVersion7();

        using var response = await GetAsync("/results-board", headers: (TestAuthentication.UserHeader, userId.ToString()));

        var logEvent = await app.Telemetry.WaitForRequestLogAsync("/results-board");
        Scalar(logEvent, RequestContextEnricher.ActorKind).ShouldBe(nameof(ActorKind.User));
        Scalar(logEvent, RequestContextEnricher.ActorId).ShouldBe(userId);
    }

    private async Task<HttpResponseMessage> GetAsync(
        string path, string? correlationId = null, params (string Name, string Value)[] headers)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return await client.SendAsync(request, Ct);
    }

    private static DefaultHttpContext Request(string path, int status) =>
        new() { Request = { Path = path }, Response = { StatusCode = status } };

    private static object? Scalar(LogEvent logEvent, string property)
    {
        logEvent.Properties.ShouldContainKey(property);
        return logEvent.Properties[property].ShouldBeOfType<ScalarValue>().Value;
    }
}
