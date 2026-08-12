using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class MetricFrameworkApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public MetricFrameworkApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task MetricEndpoints_ExposeTypesValidateValuesAndRankWithTies()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsync(client);

        var types = await client.GetFromJsonAsync<string[]>("/api/sports/metrics/types");
        types.Should().BeEquivalentTo(new[]
        {
            "Integer", "Decimal", "Duration", "ClockTime", "Distance", "Weight",
            "Boolean", "Text", "Enumeration", "CompositeScore"
        });

        var definition = new
        {
            id = "FINISH_TIME", name = "Finish time", type = "Duration", unit = "s",
            sortDirection = "Ascending",
            validation = new { minimum = 0, maximum = 60, precision = 3 }
        };
        var validatedResponse = await client.PostAsJsonAsync("/api/sports/metrics/validate", new
        {
            definition, value = "10.1236", unit = "s"
        });
        validatedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await validatedResponse.Content.ReadAsStringAsync());
        var validated = await validatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        validated.GetProperty("serializedValue").GetString().Should().Be("10.124");
        validated.GetProperty("unit").GetString().Should().Be("s");
        validated.GetProperty("precision").GetInt32().Should().Be(3);

        var first = Guid.NewGuid();
        var tied = Guid.NewGuid();
        var third = Guid.NewGuid();
        var rankResponse = await client.PostAsJsonAsync("/api/sports/metrics/rank", new
        {
            definition,
            values = new[]
            {
                new { participantId = third, value = "11.000", unit = "s" },
                new { participantId = first, value = "10.125", unit = "s" },
                new { participantId = tied, value = "10.125", unit = "s" }
            }
        });
        rankResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await rankResponse.Content.ReadAsStringAsync());
        var ranking = await rankResponse.Content.ReadFromJsonAsync<JsonElement>();
        ranking.EnumerateArray().Select(item => item.GetProperty("rank").GetInt32())
            .Should().Equal(1, 1, 3);

        var invalid = await client.PostAsJsonAsync("/api/sports/metrics/validate", new
        {
            definition, value = "61", unit = "s"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@premier.com", password = "TenantAdmin123!",
            deviceId = "metric-framework", deviceName = "Metric framework test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
