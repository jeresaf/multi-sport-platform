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
using SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.ListRulesetTemplates;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary>
/// <c>GET /api/admin/v1/ruleset-templates</c>, <c>…/ruleset-templates/{code}</c> (<c>ruleset.read</c>) and the platform
/// pair (<c>platform.sport.read</c>) over the seeded templates (S05 D11, D14, D16). Which roles may call them is
/// <c>AuthorizationMatrixTests</c>'.
/// </summary>
public sealed class RulesetTemplateEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _adminPath = "/api/admin/v1/ruleset-templates";
    private const string _platformPath = "/api/platform/v1/ruleset-templates";
    private const string _league = "FOOTBALL_LEAGUE";

    // Grace Nakato, KMSA's General Secretary (docs/product/pilot-customer.md).
    private static readonly Guid _kmsaSecretaryId = new("0192a1b2-0000-7000-8000-00000000c101");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task A_tenant_member_lists_the_published_templates_by_name_without_an_audit_entry()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri(_adminPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ListRulesetTemplatesResponse>(Json, Ct)).ShouldNotBeNull().Items
            .Select(template => (template.Code, template.Name, template.Version))
            .ShouldBe([("FOOTBALL_CUP", "Football cup", 1), (_league, "Football league", 1)]);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_template_answers_at_its_latest_version_with_its_hash_as_the_ETag()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_league}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var template = (await response.Content.ReadFromJsonAsync<RulesetTemplateResponse>(Json, Ct)).ShouldNotBeNull();
        var stored = (await QueryAsync(
            DatabaseRoles.App,
            "SELECT content_hash FROM config.ruleset_templates WHERE code = 'FOOTBALL_LEAGUE' AND version = 1",
            reader => reader.GetString(0))).ShouldHaveSingleItem();
        template.ShouldSatisfyAllConditions(
            t => t.Code.ShouldBe(_league),
            t => t.Version.ShouldBe(1),
            t => t.ContentHash.ShouldBe(stored),
            t => t.Sport.ShouldBe(new RulesetSportResponse("FOOTBALL_ASSOCIATION", 1)),
            t => t.Formats.ShouldBe(["ROUND_ROBIN", "SINGLE_CONTEST"]),
            t => t.Values.Standings.Points.ShouldBe(new StandingsPointsResponse(3, 1, 0, 0, 3, 0)));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{stored}\"");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_template_uses_the_APIs_names_and_values_keeps_paths_and_codes_and_describes_its_tiebreakers()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_league}", UriKind.Relative), Ct);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = body.RootElement;
        var parameters = root.GetProperty("parameters");
        parameters.EnumerateObject().Select(p => p.Name).ShouldBe(
        [
            "standings.points.win", "standings.tiebreakers", "contest.period_seconds.FIRST_HALF", "contest.period_seconds.SECOND_HALF",
            "lineup.bench_max", "substitutions.max", "roster.min", "roster.max",
        ], ignoreOrder: true);
        var win = parameters.GetProperty("standings.points.win");
        (win.GetProperty("type").GetString(), win.GetProperty("min").GetInt32(), win.GetProperty("max").GetInt32(), win.GetProperty("default").GetInt32())
            .ShouldBe(("NUMBER", 1, 5, 3));
        var tiebreakers = parameters.GetProperty("standings.tiebreakers");
        tiebreakers.GetProperty("type").GetString().ShouldBe("LIST");
        tiebreakers.GetProperty("default").EnumerateArray().Select(v => v.GetString()).ShouldBe(
            ["POINTS", "GOAL_DIFFERENCE", "GOALS_FOR", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS"]);

        var values = root.GetProperty("values");
        values.GetProperty("contest").GetProperty("periodSeconds").GetProperty("FIRST_HALF").GetInt32().ShouldBe(2700);
        values.GetProperty("contest").GetProperty("coverageDefault").GetString().ShouldBe("STANDARD");
        values.GetProperty("lineup").GetProperty("positions").GetProperty("GK").GetProperty("max").GetInt32().ShouldBe(1);
        values.GetProperty("roster").GetProperty("lockPolicy").GetString().ShouldBe("FIRST_CONTEST");
        values.GetProperty("standings").GetProperty("forfeitScore").GetProperty("scored").GetInt32().ShouldBe(3);
        values.GetProperty("standings").GetProperty("headToHeadReapply").GetBoolean().ShouldBeTrue();
        values.GetProperty("operations").GetProperty("readiness")[0].GetString().ShouldBe("LINEUPS_APPROVED");
        values.GetProperty("operations").GetProperty("officialsMinimum").GetProperty("REFEREE").GetInt32().ShouldBe(1);
        values.GetProperty("eligibility").GetProperty("minAge").ValueKind.ShouldBe(JsonValueKind.Null);
        values.GetProperty("eligibility").GetProperty("minorConsentRequired").GetString().ShouldBe("TENANT_DEFAULT");

        // The ones it lists or allows, in the catalogue's order: WINS is allowed, not listed; no other sport's.
        root.GetProperty("tiebreakers").EnumerateArray().Select(t => t.GetProperty("code").GetString()).ShouldBe(
            ["POINTS", "WINS", "GOAL_DIFFERENCE", "GOALS_FOR", "H2H_POINTS", "H2H_DIFFERENCE", "H2H_SCORED", "DRAWING_OF_LOTS"]);
        root.GetProperty("tiebreakers")[0].GetProperty("label").GetString().ShouldBe("Points");
    }

    [Fact]
    public async Task A_version_is_read_by_its_number()
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/FOOTBALL_CUP?version=1", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RulesetTemplateResponse>(Json, Ct)).ShouldNotBeNull().Version.ShouldBe(1);
    }

    [Theory]
    [InlineData(_league + "?version=2")]
    [InlineData("NETBALL_LEAGUE")]
    [InlineData("football_league")]
    public async Task An_unknown_template_or_version_is_not_found(string path)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{path}", UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "RULESET_TEMPLATE_NOT_FOUND");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("latest")]
    public async Task A_version_that_is_not_a_positive_whole_number_is_refused(string version)
    {
        using var client = await TenantClientAsync();

        using var response = await client.GetAsync(new Uri($"{_adminPath}/{_league}?version={version}", UriKind.Relative), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldBe(["version"]);
    }

    [Fact]
    public async Task Platform_staff_read_the_same_list_and_template()
    {
        using var tenant = await TenantClientAsync();
        App.Authorization.Allow(SportsConfigurationActions.PlatformSportRead);
        using var platform = App.CreateClient().AsUser(TenantTestData.StaffId);

        foreach (var path in new[] { string.Empty, $"/{_league}" })
        {
            using var fromTenant = await tenant.GetAsync(new Uri(_adminPath + path, UriKind.Relative), Ct);
            using var fromPlatform = await platform.GetAsync(new Uri(_platformPath + path, UriKind.Relative), Ct);

            fromPlatform.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await fromPlatform.Content.ReadAsStringAsync(Ct)).ShouldBe(await fromTenant.Content.ReadAsStringAsync(Ct));
        }

        using var notFound = await platform.GetAsync(new Uri($"{_platformPath}/NETBALL_LEAGUE", UriKind.Relative), Ct);
        await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "RULESET_TEMPLATE_NOT_FOUND");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(_adminPath)]
    [InlineData(_platformPath + "/" + _league)]
    public async Task An_anonymous_request_is_unauthenticated(string path)
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    /// <summary>A member of KMSA allowed <c>ruleset.read</c> (the base class resets the allowed actions before each test).</summary>
    private async Task<HttpClient> TenantClientAsync()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        App.Authorization.Allow(SportsConfigurationActions.RulesetRead);
        var client = App.CreateClient().AsUser(_kmsaSecretaryId, kmsa);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
        return client;
    }
}
