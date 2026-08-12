using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class SportCatalogueLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid NbaTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly ApiWebApplicationFactory _factory;

    public SportCatalogueLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PlatformAdministrator_CanCreateValidateAndManageVersionedCatalogueEntries()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsync(client, "admin@sportsplatform.com", "Admin123!", "sport-platform");
        var code = $"TEST-{Guid.NewGuid():N}";

        var invalid = await client.PostAsJsonAsync("/api/sports", new
        {
            code = "invalid code", name = "Test sport", description = "Test",
            icon = "🏅", supportedParticipantShapes = new[] { "team" }
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/sports", new
        {
            code = $"EMPTY-{Guid.NewGuid():N}", name = "Missing shapes",
            description = (string?)null, icon = (string?)null,
            supportedParticipantShapes = Array.Empty<string>()
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var create = await client.PostAsJsonAsync("/api/sports", new
        {
            code = code.ToLowerInvariant(), name = "  Test sport  ", description = "Catalogue test",
            icon = "🏅", supportedParticipantShapes = new[] { "Pair", "individual", "pair" }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var sportId = created.GetProperty("id").GetGuid();
        created.GetProperty("code").GetString().Should().Be(code.ToUpperInvariant());
        created.GetProperty("sportPackageVersion").GetInt32().Should().Be(1);
        created.GetProperty("supportedParticipantShapes").EnumerateArray()
            .Select(item => item.GetString()).Should().Equal("individual", "pair");
        (await client.GetFromJsonAsync<JsonElement>($"/api/sports/by-code/{code.ToLowerInvariant()}"))
            .GetProperty("id").GetGuid().Should().Be(sportId);

        (await client.PostAsJsonAsync("/api/sports", new
        {
            code, name = "Duplicate", description = (string?)null,
            icon = (string?)null, supportedParticipantShapes = new[] { "team" }
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var update = await client.PutAsJsonAsync($"/api/sports/{sportId}", new
        {
            name = "Test sport updated", description = "Updated", icon = "🎯",
            supportedParticipantShapes = new[] { "team", "individual" }, isActive = true
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString()
            .Should().Be("Test sport updated");

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.AuditEntries.Where(item => item.EntityId == sportId.ToString())
            .Select(item => item.Action).ToArrayAsync())
            .Should().Contain(new[] { "SportCreated", "SportUpdated" });
    }

    [Fact]
    public async Task TenantOwner_CanEnablePermittedSportWithinLimitsAndCompetitionRequiresSelection()
    {
        using var platform = _factory.CreateClient();
        await AuthorizeAsync(platform, "admin@sportsplatform.com", "Admin123!", "sport-entitlement-platform");
        var create = await platform.PostAsJsonAsync("/api/sports", new
        {
            code = $"TENANT-{Guid.NewGuid():N}", name = "Tenant selectable sport",
            description = "Tenant selection test", icon = "🎯",
            supportedParticipantShapes = new[] { "team" }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var sportId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var tenant = _factory.CreateClient();
        await AuthorizeAsync(tenant, "admin@premier.com", "TenantAdmin123!", "sport-entitlement-tenant");
        (await tenant.PostAsJsonAsync("/api/sports", new
        {
            code = "BLOCKED", name = "Blocked", description = (string?)null,
            icon = (string?)null, supportedParticipantShapes = new[] { "team" }
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenant.GetAsync($"/api/sports/tenant/{NbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var subscription = await platform.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/active/{PremierTenantId}");
        var subscriptionId = subscription.GetProperty("id").GetGuid();
        (await platform.PutAsJsonAsync($"/api/subscriptions/{subscriptionId}/limits",
            new Dictionary<string, int> { ["max_sports"] = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await tenant.PutAsJsonAsync($"/api/sports/tenant/{PremierTenantId}/{sportId}",
            new { enabled = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await platform.PutAsJsonAsync($"/api/subscriptions/{subscriptionId}/limits",
            new Dictionary<string, int> { ["max_sports"] = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var enable = await tenant.PutAsJsonAsync($"/api/sports/tenant/{PremierTenantId}/{sportId}",
            new { enabled = true });
        enable.StatusCode.Should().Be(HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());

        var competition = await tenant.PostAsJsonAsync("/api/competitions", new
        {
            name = "Tenant selected sport competition", sportId, tenantId = PremierTenantId
        });
        competition.StatusCode.Should().Be(HttpStatusCode.Created, await competition.Content.ReadAsStringAsync());

        (await tenant.PutAsJsonAsync($"/api/sports/tenant/{PremierTenantId}/{sportId}",
            new { enabled = false })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await tenant.PostAsJsonAsync("/api/competitions", new
        {
            name = "Blocked competition", sportId, tenantId = PremierTenantId
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.AuditEntries.Where(item => item.TenantId == PremierTenantId)
            .Select(item => item.Action).ToArrayAsync())
            .Should().Contain(new[] { "TenantSportEnabled", "TenantSportDisabled" });
    }

    [Fact]
    public async Task DefinitionVersions_AreValidatedClonedImmutableAuditedAndExactlyPinned()
    {
        using var platform = _factory.CreateClient();
        await AuthorizeAsync(platform, "admin@sportsplatform.com", "Admin123!", "sport-version-platform");
        var football = await platform.GetFromJsonAsync<JsonElement>("/api/sports/by-code/football");
        var sportId = football.GetProperty("id").GetGuid();

        var create = await platform.PostAsJsonAsync($"/api/sports/{sportId}/versions", new
        {
            name = $"Definition lifecycle {Guid.NewGuid():N}",
            createdBy = "untrusted-client"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var draft = await create.Content.ReadFromJsonAsync<JsonElement>();
        var sourceVersionId = draft.GetProperty("id").GetGuid();
        draft.GetProperty("isPublished").GetBoolean().Should().BeFalse();
        draft.GetProperty("createdBy").GetString().Should().NotBe("untrusted-client");

        (await platform.PostAsync($"/api/sports/versions/{sourceVersionId}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var configuration = new Dictionary<string, string>
        {
            ["contestShape"] = "TeamVsTeam",
            ["participantType"] = "Team",
            ["segmentDefinitions"] = "[{\"code\":\"half\"}]",
            ["eventDefinitions"] = "[{\"code\":\"goal\",\"name\":\"Goal\"}]",
            ["metricDefinitions"] = "[{\"code\":\"goals\",\"name\":\"Goals\",\"type\":\"Integer\",\"validation\":{\"minimum\":0}}]",
            ["resultMethods"] = "[\"score\"]",
            ["rankingRules"] = "[{\"orderBy\":\"points\"}]",
            ["displayConfiguration"] = "{\"scoreboard\":true}",
            ["validationConfiguration"] = "{\"minimumParticipants\":2}"
        };
        var invalid = new Dictionary<string, string>(configuration)
        {
            ["eventDefinitions"] = "{}"
        };
        (await platform.PutAsJsonAsync($"/api/sports/versions/{sourceVersionId}", invalid))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var configure = await platform.PutAsJsonAsync(
            $"/api/sports/versions/{sourceVersionId}", configuration);
        configure.StatusCode.Should().Be(HttpStatusCode.OK, await configure.Content.ReadAsStringAsync());
        var configured = await configure.Content.ReadFromJsonAsync<JsonElement>();
        (await platform.PostAsync($"/api/sports/versions/{sourceVersionId}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await platform.PutAsJsonAsync($"/api/sports/versions/{sourceVersionId}", configuration))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var tenant = _factory.CreateClient();
        await AuthorizeAsync(tenant, "admin@premier.com", "TenantAdmin123!", "sport-version-tenant");
        (await tenant.PostAsJsonAsync(
            $"/api/sports/{sportId}/versions/{sourceVersionId}/clone",
            new { name = "Tenant must not clone" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var cloneResponse = await platform.PostAsJsonAsync(
            $"/api/sports/{sportId}/versions/{sourceVersionId}/clone",
            new { name = "Cloned exact definition" });
        cloneResponse.StatusCode.Should().Be(HttpStatusCode.Created, await cloneResponse.Content.ReadAsStringAsync());
        var clone = await cloneResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cloneVersionId = clone.GetProperty("id").GetGuid();
        clone.GetProperty("isPublished").GetBoolean().Should().BeFalse();
        foreach (var property in new[]
                 {
                     "contestShape", "participantType", "segmentDefinitions", "eventDefinitions",
                     "metricDefinitions", "resultMethods", "rankingRules", "displayConfiguration",
                     "validationConfiguration"
                 })
            clone.GetProperty(property).GetString().Should().Be(
                configured.GetProperty(property).GetString());

        (await platform.PostAsync($"/api/sports/versions/{cloneVersionId}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var competitionResponse = await tenant.PostAsJsonAsync("/api/competitions", new
        {
            name = $"Pinned definition {Guid.NewGuid():N}", sportId, tenantId = PremierTenantId
        });
        competitionResponse.StatusCode.Should().Be(HttpStatusCode.Created, await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();
        competition.GetProperty("rulesetVersionId").GetGuid().Should().Be(cloneVersionId);
        (await tenant.PutAsync($"/api/competitions/{competitionId}/ruleset/{sourceVersionId}", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.AuditEntries.Where(item =>
                item.EntityId == sourceVersionId.ToString() || item.EntityId == cloneVersionId.ToString())
            .Select(item => item.Action).ToArrayAsync())
            .Should().Contain(new[]
            {
                "SportDefinitionVersionCreated", "SportDefinitionVersionConfigured",
                "SportDefinitionVersionPublished", "SportDefinitionVersionCloned"
            });
    }

    [Fact]
    public async Task TenantAdministrator_CanUseUniversalParticipantsAndContestTypePolicies()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsync(client, "admin@premier.com", "TenantAdmin123!", "participant-abstraction");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000103");

        async Task<JsonElement> CreateParticipant(string type)
        {
            var response = await client.PostAsJsonAsync("/api/participants", new
            {
                name = $"{type} {suffix}", shortName = $"{type[..2]}{suffix[..3]}",
                tenantId = PremierTenantId, type
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created,
                await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        var athlete = await CreateParticipant("Athlete");
        var pair = await CreateParticipant("Pair");
        var team = await CreateParticipant("Team");
        var athleteId = athlete.GetProperty("id").GetGuid();

        var attributes = await client.PutAsJsonAsync(
            $"/api/participants/{athleteId}/sports/{sportId}/attributes",
            new { attributes = new { personalBestSeconds = 10.31, lanePreference = 4 } });
        attributes.StatusCode.Should().Be(HttpStatusCode.OK,
            await attributes.Content.ReadAsStringAsync());
        var loaded = await client.GetFromJsonAsync<JsonElement>($"/api/participants/{athleteId}");
        loaded.GetProperty("sportProfiles").GetArrayLength().Should().Be(1);

        var contestResponse = await client.PostAsJsonAsync("/api/contests", new
        {
            tenantId = PremierTenantId, sportId, contestShape = "Ranked",
            name = $"Mixed entrants {suffix}",
            allowedParticipantTypes = new[] { "Athlete", "Pair" }
        });
        contestResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await contestResponse.Content.ReadAsStringAsync());
        var contestId = (await contestResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        foreach (var (participant, type) in new[] { (athlete, "Athlete"), (pair, "Pair") })
        {
            var add = await client.PostAsJsonAsync($"/api/contests/{contestId}/participants", new
            {
                participantId = participant.GetProperty("id").GetGuid(),
                participantType = type
            });
            add.StatusCode.Should().Be(HttpStatusCode.OK, await add.Content.ReadAsStringAsync());
        }

        var rejected = await client.PostAsJsonAsync($"/api/contests/{contestId}/participants", new
        {
            participantId = team.GetProperty("id").GetGuid(), participantType = "Team"
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var mismatched = await client.PostAsJsonAsync($"/api/contests/{contestId}/participants", new
        {
            participantId = athleteId, participantType = "Team"
        });
        mismatched.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task AuthorizeAsync(HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Sport catalogue lifecycle test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
