using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;

namespace SportsPlatform.IntegrationTests.Health;

public sealed class HealthReadyTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Ready_returns_ok_when_the_database_answers()
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldHaveOnlyStatusAsync(response, "ok");
    }

    [Fact]
    public async Task Ready_returns_503_with_only_a_status_when_the_database_is_unreachable()
    {
        // Port 1 on loopback refuses connections at once.
        const string password = "sp-app-wrong-secret";
        await using var unreachable = new TestApp(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringNames.App}"] =
                $"Host=127.0.0.1;Port=1;Database=sportsplatform;Username=sp_app;Password={password};Timeout=2",
        });
        using var client = unreachable.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var body = await ShouldHaveOnlyStatusAsync(response, "unavailable");
        foreach (var secret in new[] { "127.0.0.1", "sp_app", password, "Npgsql", "Exception", "refused" })
        {
            body.ShouldNotContain(secret, Case.Insensitive);
        }

        var warning = await unreachable.Telemetry.WaitForEventAsync(e => e.Exception is not null);
        warning.Level.ShouldBe(LogEventLevel.Warning);
        warning.RenderMessage(null).ShouldNotContain(password);
    }

    [Fact]
    public async Task Database_work_is_traced_but_readiness_probes_are_not()
    {
        App.Telemetry.Clear();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>().ConventionProbes.CountAsync(Ct);
        }

        var querySpan = await App.Telemetry.WaitForSpanAsync(IsDatabaseSpan);
        querySpan.Kind.ShouldBe(ActivityKind.Client);

        // The check's query has finished (and its span stopped) before the response is written.
        App.Telemetry.Clear();
        using var client = App.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Telemetry.Spans.ShouldNotContain(span => IsDatabaseSpan(span));
    }

    private static bool IsDatabaseSpan(Activity span) => span.Source.Name == "Npgsql";

    private static async Task<string> ShouldHaveOnlyStatusAsync(HttpResponseMessage response, string status)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var body = await response.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().ShouldBe(status);
        json.RootElement.EnumerateObject().Count().ShouldBe(1);
        return body;
    }
}
