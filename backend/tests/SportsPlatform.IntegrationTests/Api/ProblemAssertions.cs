using System.Net;
using System.Text.Json;
using SportsPlatform.Host.Api;

namespace SportsPlatform.IntegrationTests.Api;

internal static class ProblemAssertions
{
    /// <summary>Checks the error contract and returns the body for further checks.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonDocument.Parse(body).RootElement.Clone();
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("type").GetString().ShouldBe($"https://errors.sportsplatform.invalid/{code}");
        problem.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();

        var correlationId = problem.GetProperty("correlationId").GetString();
        correlationId.ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldBe([correlationId]);
        return problem;
    }
}
