using System.Net;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Health;

public sealed class HealthLiveTests(HostOnlyApp factory) : IClassFixture<HostOnlyApp>
{
    [Fact]
    public async Task Get_health_live_returns_ok_status()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().ShouldBe("ok");
        json.RootElement.EnumerateObject().Count().ShouldBe(1);
    }
}
