using System.Net;
using System.Text;

namespace SportsPlatform.IntegrationTests.Api;

public sealed class PipelineProblemDetailsTests(PipelineTestApp app) : IClassFixture<PipelineTestApp>
{
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
}
