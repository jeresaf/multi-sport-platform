using System.Net;
using Serilog.Events;
using SportsPlatform.Host.Api;
using SportsPlatform.Host.Observability;
using SportsPlatform.IntegrationTests.Api;

namespace SportsPlatform.IntegrationTests.Observability;

public sealed class ExceptionLoggingTests(TestingPipelineApp app) : IClassFixture<TestingPipelineApp>
{
    [Fact]
    public async Task Unhandled_exception_is_logged_with_its_details_and_correlation_ID()
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/throws", UriKind.Relative));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "kmsa-req-0500");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var error = await app.Telemetry.WaitForEventAsync(e => e.Exception is InvalidOperationException);
        error.Level.ShouldBe(LogEventLevel.Error);
        error.Exception!.Message.ShouldBe(PipelineTestApp.ExceptionSecret);
        error.Properties[RequestContextEnricher.CorrelationId].ShouldBe(new ScalarValue("kmsa-req-0500"));
        error.Properties.ShouldNotContainKey("RequestId"); // the correlation ID is the one request ID

        var completion = await app.Telemetry.WaitForRequestLogAsync("/throws");
        completion.Level.ShouldBe(LogEventLevel.Error);
        completion.Properties["StatusCode"].ShouldBe(new ScalarValue(500));
    }
}
