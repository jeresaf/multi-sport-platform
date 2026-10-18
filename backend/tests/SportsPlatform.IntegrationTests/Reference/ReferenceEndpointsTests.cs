using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Reference;

/// <summary>
/// The public reference lists on the seeded test database, with every action denied (the default after a reset): they
/// need no authorization.
/// </summary>
public sealed class ReferenceEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _base = "/api/public/v1/reference/";

    [Theory]
    [InlineData("countries", "UG", """{"code":"UG","alpha3Code":"UGA","numericCode":"800","name":"Uganda"}""")]
    [InlineData("currencies", "UGX", """{"code":"UGX","numericCode":"800","name":"Ugandan Shilling","minorUnits":0}""")]
    [InlineData("languages", "sw", """{"code":"sw","name":"Swahili"}""")]
    [InlineData("time-zones", "Africa/Kampala", """{"code":"Africa/Kampala","countryCode":"UG"}""")]
    public async Task Each_list_returns_its_entries_ordered_by_code(string list, string code, string expected)
    {
        using var client = App.CreateClient();

        var items = await ItemsAsync(client, list);

        items.Single(item => item.GetProperty("code").GetString() == code).GetRawText().ShouldBe(expected);
        var codes = items.Select(item => item.GetProperty("code").GetString()!).ToList();
        codes.ShouldBe(codes.Order(StringComparer.Ordinal).ToList());
        codes.ShouldBeUnique();
    }

    [Theory]
    [InlineData("countries", "countries", "KE")]
    [InlineData("currencies", "currencies", "KES")]
    [InlineData("languages", "languages", "lg")]
    [InlineData("time-zones", "time_zones", "Africa/Nairobi")]
    public async Task Inactive_entries_are_left_out(string list, string table, string code)
    {
        using var client = App.CreateClient();
        (await CodesAsync(client, list)).ShouldContain(code);

        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, $"UPDATE platform.{table} SET is_active = false WHERE code = '{code}'");

            (await CodesAsync(client, list)).ShouldNotContain(code);
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }
    }

    [Fact]
    public async Task Time_zones_of_one_country()
    {
        using var client = App.CreateClient();

        (await CodesAsync(client, "time-zones?country=UG")).ShouldBe(["Africa/Kampala"]);
        (await CodesAsync(client, "time-zones?country=US")).ShouldContain("America/New_York");
        (await CodesAsync(client, "time-zones?country=US")).ShouldNotContain("Africa/Kampala");
        (await CodesAsync(client, "time-zones?country=ZZ")).ShouldBeEmpty(); // a valid code without zones
        (await CodesAsync(client, "time-zones")).ShouldContain("Etc/UTC");
    }

    [Theory]
    [InlineData("ug")]
    [InlineData("UGA")]
    [InlineData("U")]
    [InlineData("")]
    public async Task A_country_that_is_not_an_upper_case_alpha2_code_is_rejected(string country)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync($"{_base}time-zones?country={country}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        response.Headers.CacheControl.ShouldBeNull();
        response.Headers.ETag.ShouldBeNull();
    }

    [Theory]
    [InlineData("countries")]
    [InlineData("currencies")]
    [InlineData("languages")]
    [InlineData("time-zones")]
    [InlineData("time-zones?country=UG")]
    public async Task Lists_are_cacheable_for_an_hour_with_a_strong_ETag(string list)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(_base + list, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe("public, max-age=3600");
        var etag = response.Headers.ETag.ShouldNotBeNull();
        etag.IsWeak.ShouldBeFalse();
        using var again = await GetAsync(client, list, ifNoneMatch: null);
        again.Headers.ETag.ShouldBe(etag); // same data, same tag
    }

    [Fact]
    public async Task A_matching_If_None_Match_gets_304_without_a_body()
    {
        using var client = App.CreateClient();
        using var first = await client.GetAsync(_base + "countries", Ct);
        var etag = first.Headers.ETag.ShouldNotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, _base + "countries");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"another\""));
        request.Headers.IfNoneMatch.Add(etag);
        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBeEmpty();
        response.Headers.ETag.ShouldBe(etag);
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe("public, max-age=3600");
    }

    [Fact]
    public async Task A_stale_ETag_gets_the_list_again_and_the_tag_changes_with_the_data()
    {
        using var client = App.CreateClient();
        using var before = await client.GetAsync(_base + "countries", Ct);
        var etag = before.Headers.ETag.ShouldNotBeNull();

        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, "UPDATE platform.countries SET is_active = false WHERE code = 'KE'");

            using var response = await GetAsync(client, "countries", etag);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.ETag.ShouldNotBeNull().ShouldNotBe(etag);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("\"KE\"");
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string list, EntityTagHeaderValue? ifNoneMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _base + list);
        if (ifNoneMatch is not null)
        {
            request.Headers.IfNoneMatch.Add(ifNoneMatch);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<List<JsonElement>> ItemsAsync(HttpClient client, string list)
    {
        using var response = await client.GetAsync(_base + list, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["items"]);
        return [.. body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private static async Task<List<string>> CodesAsync(HttpClient client, string list) =>
        [.. (await ItemsAsync(client, list)).Select(item => item.GetProperty("code").GetString()!)];
}
