using System.Net;
using System.Text;
using SportsPlatform.Host.Api;

namespace SportsPlatform.IntegrationTests.Api;

public abstract class PipelineProblemDetailsTests(PipelineTestApp app)
{
    [Fact]
    public async Task Correlation_id_reaches_the_requests_correlation_context()
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/correlation", UriKind.Relative));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "kmsa-fixtures.17");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("\"kmsa-fixtures.17\"");
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_without_internals()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/throws", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "PLATFORM_UNEXPECTED_ERROR");
        var body = problem.GetRawText();
        body.ShouldNotContain(PipelineTestApp.ExceptionSecret);
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("at SportsPlatform");
        problem.TryGetProperty("detail", out _).ShouldBeFalse();
        problem.TryGetProperty("exception", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Result_failure_returns_its_status_and_code()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/conflict", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PLATFORM_CONCURRENCY_CONFLICT");
        problem.GetProperty("title").GetString().ShouldBe("The record was changed by someone else. Reload and try again.");
    }

    [Fact]
    public async Task Number_sent_as_a_string_returns_400()
    {
        using var client = app.CreateClient();
        using var content = new StringContent("{\"number\": \"5\"}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/rounds", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Number_sent_as_a_number_is_accepted()
    {
        using var client = app.CreateClient();
        using var content = new StringContent("{\"number\": 5}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/rounds", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malformed_json_returns_400()
    {
        using var client = app.CreateClient();
        using var content = new StringContent("{\"homeTeam\": \"Ntinda Swifts FC", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/fixtures", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Non_json_body_returns_415()
    {
        using var client = app.CreateClient();
        using var content = new StringContent("homeTeam=Ntinda Swifts FC", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(new Uri("/fixtures", UriKind.Relative), content, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "VALIDATION_UNSUPPORTED_MEDIA_TYPE");
    }

    [Fact]
    public async Task Wrong_method_returns_405()
    {
        using var client = app.CreateClient();

        using var response = await client.DeleteAsync(new Uri("/fixtures", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "PLATFORM_METHOD_NOT_ALLOWED");
    }

    // Endpoints return failures through ToProblem(); a bare 404 from a matched route must not claim "no route".
    [Fact]
    public async Task Bare_not_found_from_a_matched_endpoint_is_not_route_not_found()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/bare-not-found", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("PLATFORM_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Problem_has_one_request_id()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/conflict", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PLATFORM_CONCURRENCY_CONFLICT");
        problem.TryGetProperty("traceId", out _).ShouldBeFalse();
    }
}

public sealed class TestingPipelineProblemDetailsTests(TestingPipelineApp app)
    : PipelineProblemDetailsTests(app), IClassFixture<TestingPipelineApp>;

public sealed class DevelopmentPipelineProblemDetailsTests(DevelopmentPipelineApp app)
    : PipelineProblemDetailsTests(app), IClassFixture<DevelopmentPipelineApp>;
