using System.Net;
using SportsPlatform.Host.Api;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Api;

/// <summary>The error contract and correlation ID on the real Host.</summary>
public sealed class HostProblemDetailsTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    // S01-T3 acceptance: a sample failing request returns Problem Details with code and correlationId.
    [Fact]
    public async Task Unknown_route_returns_problem_details_with_code_and_correlation_id()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/public/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_ROUTE_NOT_FOUND");
        problem.GetProperty("title").GetString().ShouldBe("No endpoint matches this path.");
    }

    [Fact]
    public async Task Well_formed_incoming_correlation_id_is_kept()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/admin/v1/nothing-here", UriKind.Relative));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "kmsa-scorer.42_retry-1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PLATFORM_ROUTE_NOT_FOUND");
        problem.GetProperty("correlationId").GetString().ShouldBe("kmsa-scorer.42_retry-1");
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("new\\nline")]
    [InlineData("<script>")]
    [InlineData("an-id-that-is-far-too-long-to-be-accepted-as-a-correlation-identifier-x")]
    public async Task Malformed_incoming_correlation_id_is_replaced(string incoming)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, incoming);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldHaveSingleItem();
        correlationId.ShouldNotBe(incoming);
        Guid.Parse(correlationId).Version.ShouldBe(7);
    }

    [Fact]
    public async Task Successful_responses_carry_a_generated_correlation_id()
    {
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        var firstId = first.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldHaveSingleItem();
        var secondId = second.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldHaveSingleItem();
        Guid.Parse(firstId).Version.ShouldBe(7);
        secondId.ShouldNotBe(firstId);
    }
}
