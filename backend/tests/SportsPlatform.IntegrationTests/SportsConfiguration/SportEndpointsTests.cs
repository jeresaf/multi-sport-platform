using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports.ListSports;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary>
/// <c>GET /api/admin/v1/sports</c>, <c>…/sports/{variant}</c> (<c>sport.read</c>) and the platform pair
/// (<c>platform.sport.read</c>) over the seeded definitions (S05 D14, D16). Which roles may call them is
/// <c>AuthorizationMatrixTests</c>'.
/// </summary>
public sealed class SportEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _adminPath = "/api/admin/v1/sports";
    private const string _platformPath = "/api/platform/v1/sports";
    private const string _variant = "FOOTBALL_ASSOCIATION";

    // Grace Nakato, KMSA's General Secretary (docs/product/pilot-customer.md).
    private static readonly Guid _kmsaSecretaryId = new("0192a1b2-0000-7000-8000-00000000c101");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task A_tenant_member_lists_the_published_variants_without_an_audit_entry()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri(_adminPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ListSportsResponse>(Json, Ct)).ShouldNotBeNull().Items
            .ShouldBe([new SportVariantResponse(_variant, "FOOTBALL", "Football", "ADDITIVE", ContestShape.HeadToHead, 1)]);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_definition_answers_at_its_latest_version_with_its_hash_as_the_ETag()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_variant}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var definition = (await response.Content.ReadFromJsonAsync<SportDefinitionResponse>(Json, Ct)).ShouldNotBeNull();
        var stored = (await QueryAsync(
            DatabaseRoles.App,
            "SELECT content_hash FROM config.sport_definitions WHERE variant = 'FOOTBALL_ASSOCIATION' AND version = 1",
            reader => reader.GetString(0))).ShouldHaveSingleItem();
        definition.ShouldSatisfyAllConditions(
            d => d.Variant.ShouldBe(_variant),
            d => d.Version.ShouldBe(1),
            d => d.ContentHash.ShouldBe(stored),
            d => d.Terminology.ShouldBe(new SportTerminologyResponse("Match", "Matches", "Half", "Team", "Player", "Goal")),
            d => d.GenericEventTypes.ShouldNotBeEmpty());
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{stored}\"");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_definition_uses_the_APIs_names_and_values()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_variant}", UriKind.Relative), Ct);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = body.RootElement;
        root.GetProperty("shape").GetString().ShouldBe("HEAD_TO_HEAD");
        root.GetProperty("periods")[0].GetProperty("shortLabel").GetString().ShouldBe("1H");
        root.GetProperty("periods")[0].GetProperty("clock").GetString().ShouldBe("COUNT_UP");
        var goal = root.GetProperty("eventTypes").EnumerateArray().Single(e => e.GetProperty("code").GetString() == "GOAL");
        goal.GetProperty("metadata").GetProperty("body_part").GetProperty("type").GetString().ShouldBe("CHOICE");
        root.GetProperty("actorRoles").EnumerateObject().Select(p => p.Name).ShouldContain("SCORER");
    }

    [Fact]
    public async Task A_version_is_read_by_its_number()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_variant}?version=1", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SportDefinitionResponse>(Json, Ct)).ShouldNotBeNull().Version.ShouldBe(1);
    }

    [Theory]
    [InlineData(_variant + "?version=2")]
    [InlineData("CURLING")]
    [InlineData("football_association")]
    public async Task An_unknown_variant_or_version_is_not_found(string path)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{path}", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "SPORT_DEFINITION_NOT_FOUND");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("latest")]
    public async Task A_version_that_is_not_a_positive_whole_number_is_refused(string version)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_variant}?version={version}", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldBe(["version"]);
    }

    [Fact]
    public async Task Platform_staff_read_the_same_list_and_definition()
    {
        using var tenant = await TenantClientAsync();
        App.Authorization.Allow(SportsConfigurationActions.PlatformSportRead);
        using var platform = App.CreateClient().AsUser(TenantTestData.StaffId);

        foreach (var path in new[] { string.Empty, $"/{_variant}" })
        {
            using var fromTenant = await tenant.GetAsync(new Uri(_adminPath + path, UriKind.Relative), Ct);
            using var fromPlatform = await platform.GetAsync(new Uri(_platformPath + path, UriKind.Relative), Ct);

            fromPlatform.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await fromPlatform.Content.ReadAsStringAsync(Ct)).ShouldBe(await fromTenant.Content.ReadAsStringAsync(Ct));
        }

        using var notFound = await platform.GetAsync(new Uri($"{_platformPath}/CURLING", UriKind.Relative), Ct);
        await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "SPORT_DEFINITION_NOT_FOUND");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(_adminPath)]
    [InlineData(_platformPath + "/" + _variant)]
    public async Task An_anonymous_request_is_unauthenticated(string path)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    /// <summary>A member of KMSA allowed <c>sport.read</c> (the base class resets the allowed actions before each test).</summary>
    private async Task<HttpClient> TenantClientAsync()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        App.Authorization.Allow(SportsConfigurationActions.SportRead);
        var client = App.CreateClient().AsUser(_kmsaSecretaryId, kmsa);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
        return client;
    }
}
