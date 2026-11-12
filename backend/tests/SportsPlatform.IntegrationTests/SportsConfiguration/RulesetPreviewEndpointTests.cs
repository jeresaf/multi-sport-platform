using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.PreviewRuleset;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.SportsConfiguration;

/// <summary>
/// <c>POST /api/admin/v1/ruleset-templates/{code}/preview</c> (<c>ruleset.read</c>) over the seeded templates, formats
/// and football definition (S05 D11, D16). S05-T4's acceptance through the API: the same inputs give the same hash, and
/// every invalid combination is refused with its code on the field it names. The resolver's own cases, including a tie
/// resolution the seeded templates don't let a competition change (<c>RULESET_TIE_RESOLUTION_UNSUPPORTED</c>), are
/// <c>RulesetResolverTests</c>'. Which roles may call it is <c>AuthorizationMatrixTests</c>'.
/// </summary>
public sealed class RulesetPreviewEndpointTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _path = "/api/admin/v1/ruleset-templates";

    // The slice demo: KMSA's league with 4 points for a win, drawn home and away, over the seeded football@1. The same
    // hash RulesetResolverTests pins for the resolver alone: the API adds nothing to the snapshot.
    private const string _demoHash = "sha256:b198a41e3e0eb951c898f1f09fd67800c78286a6f2a8b4d574f7efe7891c0181";

    private const string _demo = """
        { "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": 2 } }, "overrides": { "standings.points.win": 4 } }
        """;

    // Grace Nakato, KMSA's General Secretary (docs/product/pilot-customer.md).
    private static readonly Guid _kmsaSecretaryId = new("0192a1b2-0000-7000-8000-00000000c101");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task The_league_with_4_points_for_a_win_resolves_to_its_pinned_hash_without_saving_or_auditing()
    {
        using var client = await TenantClientAsync();
        var sportHash = (await QueryAsync(
            DatabaseRoles.App,
            "SELECT content_hash FROM config.sport_definitions WHERE variant = 'FOOTBALL_ASSOCIATION' AND version = 1",
            reader => reader.GetString(0))).ShouldHaveSingleItem();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", _demo);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = (await response.Content.ReadFromJsonAsync<RulesSnapshotResponse>(Json, Ct)).ShouldNotBeNull();
        snapshot.ShouldSatisfyAllConditions(
            () => snapshot.Hash.ShouldBe(_demoHash),
            () => snapshot.Sport.ShouldBe(new RulesSnapshotSportResponse("FOOTBALL_ASSOCIATION", 1, sportHash)),
            () => snapshot.Template.ShouldBe(new RulesSnapshotTemplateResponse("FOOTBALL_LEAGUE", 1)),
            () => (snapshot.Format.Code, snapshot.Format.Version).ShouldBe(("ROUND_ROBIN", 1)),
            () => snapshot.Format.Parameters.ShouldBe(new Dictionary<string, ParameterValue>
            {
                ["cycles"] = ParameterValue.Of(2),
                ["home_away_balance"] = ParameterValue.Of(true),
                ["seeding"] = ParameterValue.Of("RANDOM"),
            }),
            () => snapshot.Values.Standings.Points.Win.ShouldBe(4),
            () => snapshot.Values.Standings.Points.Draw.ShouldBe(1));
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_same_inputs_give_the_same_hash_whatever_their_order_and_a_change_gives_another()
    {
        using var client = await TenantClientAsync();

        var first = await HashAsync(client, """
            { "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": 2, "seeding": "RANDOM" } },
              "overrides": { "standings.points.win": 4, "roster.max": 35 } }
            """);
        var reordered = await HashAsync(client, """
            { "overrides": { "roster.max": 35, "standings.points.win": 4 },
              "format": { "parameters": { "seeding": "RANDOM", "cycles": 2 }, "version": 1, "code": "ROUND_ROBIN" } }
            """);
        var changed = await HashAsync(client, """
            { "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": 2, "seeding": "RANDOM" } },
              "overrides": { "standings.points.win": 4, "roster.max": 36 } }
            """);

        reordered.ShouldBe(first);
        changed.ShouldNotBe(first);
    }

    [Fact]
    public async Task A_template_is_resolved_at_the_version_asked_for()
    {
        using var client = await TenantClientAsync();

        var latest = await HashAsync(client, _demo);
        var first = await HashAsync(client, _demo, "?version=1");

        first.ShouldBe(latest);
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task An_invalid_combination_is_refused_with_its_code_on_the_field_it_names(string name)
    {
        var (template, body, expected) = _invalid[name];
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, template, body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, expected[0].Code);
        problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()))
            .ShouldBe(expected.Select(e => ((string?)e.Field, (string?)e.Code)));
        problem.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Every_problem_is_listed_and_the_answer_takes_the_first_fields_code()
    {
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", """
            { "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": 9 } },
              "overrides": { "standings.points.win": 6, "lineup.starters": 7, "roster.max": null } }
            """);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "FORMAT_PARAMETER_INVALID");
        problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()))
            .ShouldBe(
            [
                ("format.parameters.cycles", "FORMAT_PARAMETER_INVALID"),
                ("overrides.lineup.starters", "RULESET_PARAMETER_UNKNOWN"),
                ("overrides.roster.max", "RULESET_PARAMETER_OUT_OF_RANGE"),
                ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE"),
            ]);
    }

    [Theory]
    [InlineData("NETBALL_LEAGUE", "")]
    [InlineData("football_league", "")]
    [InlineData("FOOTBALL_LEAGUE", "?version=9")]
    public async Task An_unknown_template_or_version_is_not_found(string code, string query)
    {
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, code, _demo, query);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "RULESET_TEMPLATE_NOT_FOUND");
        problem.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("latest")]
    public async Task A_version_that_is_not_a_positive_whole_number_is_refused(string version)
    {
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", _demo, $"?version={version}");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe(["version"]);
    }

    [Theory]
    [InlineData("""{ "overrides": { "standings.points.win": 4 } }""", "format")]
    [InlineData("""{ "format": { "parameters": { "cycles": 2 } } }""", "format.code")]
    [InlineData("""{ "format": { "code": " " } }""", "format.code")]
    [InlineData("""{ "format": { "code": "ROUND_ROBIN", "version": 0 } }""", "format.version")]
    [InlineData("""{ "format": { "code": "ROUND_ROBIN" }, "template": "FOOTBALL_CUP" }""", "template")]
    [InlineData("""{ "format": { "code": "ROUND_ROBIN", "legs": 2 } }""", "format")]
    [InlineData("""{ "format": { "code": "ROUND_ROBIN" }, "overrides": [4] }""", "overrides")]
    public async Task A_body_of_the_wrong_shape_is_refused_on_its_field(string body, string field)
    {
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([field]);
    }

    [Fact]
    public async Task A_body_that_is_not_a_JSON_object_is_malformed()
    {
        using var client = await TenantClientAsync();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", "[]");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task A_member_without_ruleset_read_is_forbidden()
    {
        using var client = await TenantClientAsync(allowed: false);

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", _demo);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task An_anonymous_request_is_unauthenticated()
    {
        using var client = App.CreateClient();

        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", _demo);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    public static TheoryData<string> InvalidCases => [.. _invalid.Keys];

    private sealed record Case(string Template, string Body, (string Field, string Code)[] Expected);

    // One row per refusal the seeded templates can reach; the body sends one change at a time to the league (or cup).
    private static readonly Dictionary<string, Case> _invalid = new(StringComparer.Ordinal)
    {
        ["a path the template doesn't have"] = League("""{ "standings.points.bonus": 1 }""",
            ("overrides.standings.points.bonus", "RULESET_PARAMETER_UNKNOWN")),
        ["a value the template doesn't let a competition change"] = League("""{ "lineup.starters": 7 }""",
            ("overrides.lineup.starters", "RULESET_PARAMETER_UNKNOWN")),
        ["a number above its bound"] = League("""{ "standings.points.win": 6 }""",
            ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a number below its bound"] = League("""{ "contest.period_seconds.FIRST_HALF": 300 }""",
            ("overrides.contest.period_seconds.FIRST_HALF", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a value of the wrong type"] = League("""{ "standings.points.win": "FOUR" }""",
            ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a value that is no parameter value at all"] = League("""{ "standings.points.win": 3.5 }""",
            ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a null value"] = League("""{ "standings.points.win": null }""",
            ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["an object as a value"] = League("""{ "standings.points.win": { "points": 4 } }""",
            ("overrides.standings.points.win", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a smallest squad above the largest"] = League("""{ "roster.min": 40 }""",
            ("overrides.roster.min", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a tiebreaker the template doesn't offer"] = League("""{ "standings.tiebreakers": ["POINTS", "DIFFERENCE"] }""",
            ("overrides.standings.tiebreakers", "RULESET_PARAMETER_OUT_OF_RANGE")),
        ["a tiebreaker not in the catalogue"] = League("""{ "standings.tiebreakers": ["POINTS", "FAIR_PLAY"] }""",
            ("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")),
        ["a tiebreaker for another sport"] = League("""{ "standings.tiebreakers": ["POINTS", "TRIES_SCORED"] }""",
            ("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")),
        ["a tiebreaker listed twice"] = League("""{ "standings.tiebreakers": ["POINTS", "WINS", "POINTS"] }""",
            ("overrides.standings.tiebreakers", "RULESET_UNKNOWN_TIEBREAKER")),
        ["a format the template doesn't list"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "KNOCKOUT" } }""",
            [("format.code", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format the cup doesn't list"] = new("FOOTBALL_CUP", """{ "format": { "code": "ROUND_ROBIN" } }""",
            [("format.code", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format that doesn't exist"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "SWISS" } }""",
            [("format.code", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format version that doesn't exist"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "ROUND_ROBIN", "version": 9 } }""",
            [("format.version", "RULESET_INCOMPATIBLE_FORMAT")]),
        ["a format parameter out of range"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": 9 } } }""",
            [("format.parameters.cycles", "FORMAT_PARAMETER_INVALID")]),
        ["a format parameter the format doesn't have"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "ROUND_ROBIN", "parameters": { "legs": 2 } } }""",
            [("format.parameters.legs", "FORMAT_PARAMETER_INVALID")]),
        ["a format parameter that is no parameter value at all"] = new("FOOTBALL_LEAGUE", """{ "format": { "code": "ROUND_ROBIN", "parameters": { "cycles": null } } }""",
            [("format.parameters.cycles", "FORMAT_PARAMETER_INVALID")]),
    };

    private static Case League(string overrides, (string Field, string Code) expected) =>
        new("FOOTBALL_LEAGUE", $$"""{ "format": { "code": "ROUND_ROBIN" }, "overrides": {{overrides}} }""", [expected]);

    private static string[] Fields(JsonElement problem) =>
        [.. problem.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("field").GetString()!)];

    private static async Task<HttpResponseMessage> PreviewAsync(HttpClient client, string code, string body, string query = "")
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri($"{_path}/{code}/preview{query}", UriKind.Relative), content, Ct);
    }

    private static async Task<string> HashAsync(HttpClient client, string body, string query = "")
    {
        using var response = await PreviewAsync(client, "FOOTBALL_LEAGUE", body, query);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<RulesSnapshotResponse>(Json, Ct)).ShouldNotBeNull().Hash;
    }

    /// <summary>A member of KMSA, allowed <c>ruleset.read</c> unless <paramref name="allowed"/> is false.</summary>
    private async Task<HttpClient> TenantClientAsync(bool allowed = true)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        if (allowed)
        {
            App.Authorization.Allow(SportsConfigurationActions.RulesetRead);
        }

        var client = App.CreateClient().AsUser(_kmsaSecretaryId, kmsa);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
        return client;
    }
}
