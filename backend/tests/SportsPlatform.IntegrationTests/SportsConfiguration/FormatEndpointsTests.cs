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
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Features.Formats;
using SportsPlatform.Modules.SportsConfiguration.Features.Formats.ListFormats;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary>
/// <c>GET /api/admin/v1/formats</c>, <c>…/formats/{code}</c> (<c>format.read</c>) and the platform pair
/// (<c>platform.sport.read</c>) over the seeded formats (S05 D10, D14, D16). Which roles may call them is
/// <c>AuthorizationMatrixTests</c>'.
/// </summary>
public sealed class FormatEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _adminPath = "/api/admin/v1/formats";
    private const string _platformPath = "/api/platform/v1/formats";
    private const string _roundRobin = "ROUND_ROBIN";

    // Grace Nakato, KMSA's General Secretary (docs/product/pilot-customer.md).
    private static readonly Guid _kmsaSecretaryId = new("0192a1b2-0000-7000-8000-00000000c101");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task A_tenant_member_lists_the_published_formats_by_name_without_an_audit_entry()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri(_adminPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ListFormatsResponse>(Json, Ct)).ShouldNotBeNull().Items
            .Select(format => (format.Code, format.Name, format.Version))
            .ShouldBe([("KNOCKOUT", "Knockout", 1), (_roundRobin, "League (round robin)", 1), ("SINGLE_CONTEST", "Single contest", 1)]);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_format_answers_at_its_latest_version_with_its_hash_as_the_ETag()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_roundRobin}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var format = (await response.Content.ReadFromJsonAsync<FormatDefinitionResponse>(Json, Ct)).ShouldNotBeNull();
        var stored = (await QueryAsync(
            DatabaseRoles.App,
            "SELECT content_hash FROM config.format_definitions WHERE code = 'ROUND_ROBIN' AND version = 1",
            reader => reader.GetString(0))).ShouldHaveSingleItem();
        format.ShouldSatisfyAllConditions(
            f => f.Code.ShouldBe(_roundRobin),
            f => f.Version.ShouldBe(1),
            f => f.ContentHash.ShouldBe(stored),
            f => f.Participants.ShouldBe(new FormatParticipantsResponse(2, 40)),
            f => f.Stages.ShouldBe([new FormatStageResponse("LEAGUE", StageType.League, FormatGeneratorKinds.RoundRobin)]));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{stored}\"");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_format_uses_the_APIs_names_and_values_and_bare_defaults()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_roundRobin}", UriKind.Relative), Ct);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = body.RootElement;
        root.GetProperty("compatibleShapes")[0].GetString().ShouldBe("HEAD_TO_HEAD");
        root.GetProperty("stages")[0].GetProperty("type").GetString().ShouldBe("LEAGUE");
        var parameters = root.GetProperty("parameters");
        parameters.EnumerateObject().Select(p => p.Name).ShouldBe(["cycles", "home_away_balance", "seeding"], ignoreOrder: true);
        var cycles = parameters.GetProperty("cycles");
        (cycles.GetProperty("type").GetString(), cycles.GetProperty("min").GetInt32(), cycles.GetProperty("max").GetInt32(), cycles.GetProperty("default").GetInt32())
            .ShouldBe(("NUMBER", 1, 4, 1));
        parameters.GetProperty("home_away_balance").GetProperty("type").GetString().ShouldBe("FLAG");
        parameters.GetProperty("home_away_balance").GetProperty("default").GetBoolean().ShouldBeTrue();
        var seeding = parameters.GetProperty("seeding");
        (seeding.GetProperty("type").GetString(), seeding.GetProperty("default").GetString()).ShouldBe(("CHOICE", "RANDOM"));
        seeding.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ShouldBe(["SEEDED", "RANDOM", "MANUAL"]);
    }

    [Fact]
    public async Task A_version_is_read_by_its_number()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/KNOCKOUT?version=1", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<FormatDefinitionResponse>(Json, Ct)).ShouldNotBeNull().Version.ShouldBe(1);
    }

    [Theory]
    [InlineData(_roundRobin + "?version=2")]
    [InlineData("SWISS")]
    [InlineData("round_robin")]
    public async Task An_unknown_format_or_version_is_not_found(string path)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{path}", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "FORMAT_DEFINITION_NOT_FOUND");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("latest")]
    public async Task A_version_that_is_not_a_positive_whole_number_is_refused(string version)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_roundRobin}?version={version}", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldBe(["version"]);
    }

    [Fact]
    public async Task Platform_staff_read_the_same_list_and_format()
    {
        using var tenant = await TenantClientAsync();
        App.Authorization.Allow(SportsConfigurationActions.PlatformSportRead);
        using var platform = App.CreateClient().AsUser(TenantTestData.StaffId);

        foreach (var path in new[] { string.Empty, $"/{_roundRobin}" })
        {
            using var fromTenant = await tenant.GetAsync(new Uri(_adminPath + path, UriKind.Relative), Ct);
            using var fromPlatform = await platform.GetAsync(new Uri(_platformPath + path, UriKind.Relative), Ct);

            fromPlatform.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await fromPlatform.Content.ReadAsStringAsync(Ct)).ShouldBe(await fromTenant.Content.ReadAsStringAsync(Ct));
        }

        using var notFound = await platform.GetAsync(new Uri($"{_platformPath}/SWISS", UriKind.Relative), Ct);
        await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "FORMAT_DEFINITION_NOT_FOUND");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(_adminPath)]
    [InlineData(_platformPath + "/" + _roundRobin)]
    public async Task An_anonymous_request_is_unauthenticated(string path)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    /// <summary>A member of KMSA allowed <c>format.read</c> (the base class resets the allowed actions before each test).</summary>
    private async Task<HttpClient> TenantClientAsync()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        App.Authorization.Allow(SportsConfigurationActions.FormatRead);
        var client = App.CreateClient().AsUser(_kmsaSecretaryId, kmsa);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
        return client;
    }
}
