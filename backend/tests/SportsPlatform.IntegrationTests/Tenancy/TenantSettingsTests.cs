using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Features.Settings;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// <c>GET</c>/<c>PATCH /api/admin/v1/tenant/settings</c> (<c>tenant.settings.read</c>, <c>tenant.settings.update</c>):
/// the settings of the tenant in <c>X-Tenant-Id</c>, for a member of it.
/// </summary>
public sealed class TenantSettingsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public const string SettingsPath = "/api/admin/v1/tenant/settings";

    private const string _tenantHeader = "X-Tenant-Id";

    // Grace Nakato, KMSA's General Secretary (docs/product/pilot-customer.md), and a member of ELSSC's staff.
    private static readonly Guid _kmsaSecretaryId = new("0192a1b2-0000-7000-8000-00000000c101");
    private static readonly Guid _elsscCoordinatorId = new("0192a1b2-0000-7000-8000-00000000c102");

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task Get_returns_the_tenants_settings_with_their_version()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var stored = await TenantTestData.SettingsAsync(App.Services, kmsa, Ct);

        using var response = await GetAsync(kmsa, _kmsaSecretaryId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = (await response.Content.ReadFromJsonAsync<TenantSettingsResponse>(Json, Ct)).ShouldNotBeNull();
        settings.ShouldBe(new TenantSettingsResponse(
            "Africa/Kampala", "en", "UGX", PublicEnabled: false, StartingSoonMinutes: 30, MinorRegistrationConsent.Recommended,
            settings.UpdatedAt, settings.Version));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{settings.Version}\"");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.settings.read"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(kmsa),
            e => e.EntityType.ShouldBe("TenantSettings"),
            e => e.EntityId.ShouldBe(stored.Id));
    }

    [Fact]
    public async Task Patch_changes_only_the_keys_sent()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await ReadAsync(kmsa, _kmsaSecretaryId);
        App.Clock.Advance(TimeSpan.FromHours(3));

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"startingSoonMinutes":45,"minorRegistrationConsent":"REQUIRED"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = (await response.Content.ReadFromJsonAsync<TenantSettingsResponse>(Json, Ct)).ShouldNotBeNull();
        after.ShouldBe(before with
        {
            StartingSoonMinutes = 45,
            MinorRegistrationConsent = MinorRegistrationConsent.Required,
            UpdatedAt = App.Clock.GetUtcNow(),
            Version = after.Version,
        });
        after.Version.ShouldNotBe(before.Version);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{after.Version}\"");
        (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldBe(after);
    }

    [Fact]
    public async Task Patch_is_audited_under_the_tenant_with_the_names_of_the_keys_sent()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var stored = await TenantTestData.SettingsAsync(App.Services, kmsa, Ct);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"timeZone":"Africa/Nairobi","publicEnabled":true}""");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await App.Services.GetRequiredService<OutboxProcessor>().ProcessOnceAsync(Ct);

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.settings.update"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(kmsa),
            e => e.ActorId.ShouldBe(_kmsaSecretaryId),
            e => e.EntityType.ShouldBe("TenantSettings"),
            e => e.EntityId.ShouldBe(stored.Id));
        JsonDocument.Parse(entry.Metadata.ShouldNotBeNull()).RootElement.GetProperty("fields").EnumerateArray()
            .Select(f => f.GetString()).ShouldBe(["timeZone", "publicEnabled"]);
    }

    [Fact]
    public async Task A_change_that_changes_nothing_saves_nothing()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, $$"""{"currencyCode":"UGX","version":{{before.Version}}}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TenantSettingsResponse>(Json, Ct)).ShouldBe(before);
    }

    [Fact]
    public async Task The_current_version_is_accepted_and_a_stale_one_conflicts()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var read = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var first = await PatchAsync(kmsa, _kmsaSecretaryId, $$"""{"startingSoonMinutes":60,"version":{{read.Version}}}""");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await PatchAsync(kmsa, _kmsaSecretaryId, $$"""{"startingSoonMinutes":15,"version":{{read.Version}}}""");
        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_CONCURRENCY_CONFLICT");
        (await ReadAsync(kmsa, _kmsaSecretaryId)).StartingSoonMinutes.ShouldBe(60);
    }

    [Theory]
    [InlineData(0u, HttpStatusCode.OK)]
    [InlineData(1u, HttpStatusCode.Conflict)]
    public async Task If_match_is_evaluated(uint offset, HttpStatusCode expected)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var read = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"startingSoonMinutes":60}""", ifMatch: $"\"{read.Version + offset}\"");

        if (expected == HttpStatusCode.OK)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ReadAsync(kmsa, _kmsaSecretaryId)).StartingSoonMinutes.ShouldBe(60);
        }
        else
        {
            await response.ShouldBeProblemAsync(expected, "TENANT_CONCURRENCY_CONFLICT");
            (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldBe(read);
        }
    }

    [Fact]
    public async Task Turning_the_public_site_on_shows_it_at_once()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        (await App.Server.GetAsync(PublicRequests.SitePath, "kmsa.localhost"))
            .ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE"); // and cached with the host

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"publicEnabled":true}""");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await App.Server.GetAsync(PublicRequests.SitePath, "kmsa.localhost")).ShouldBeSite("kmsa");
    }

    [Theory]
    [InlineData("""{"slug":"kampala-metro"}""", "slug")] // not a setting
    [InlineData("""{"matchDuration":90}""", "matchDuration")]
    [InlineData("""{"startingSoonMinutes":"30"}""", "startingSoonMinutes")]
    [InlineData("""{"startingSoonMinutes":30.5}""", "startingSoonMinutes")]
    [InlineData("""{"minorRegistrationConsent":"MAYBE"}""", "minorRegistrationConsent")]
    [InlineData("""{"publicEnabled":"yes"}""", "publicEnabled")]
    [InlineData("""{"timeZone":3}""", "timeZone")]
    [InlineData("""{"version":-1}""", "version")]
    [InlineData("""{"publicEnabled":true,"publicEnabled":false}""", "publicEnabled")] // sent twice
    public async Task Unknown_keys_and_wrongly_typed_values_are_named(string body, string field)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        CreateTenantTests.Errors(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
        (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldBe(before);
    }

    [Fact]
    public async Task Every_unknown_or_wrongly_typed_key_is_named_and_the_refusal_is_audited()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"colours":["maroon"],"startingSoonMinutes":"soon","currencyCode":"UGX"}""");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        CreateTenantTests.Errors(problem).ShouldBe([("colours", "VALIDATION_INVALID_REQUEST"), ("startingSoonMinutes", "VALIDATION_INVALID_REQUEST")]);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.settings.update"),
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.ErrorCode.ShouldBe("VALIDATION_INVALID_REQUEST"),
            e => e.TenantId.ShouldBe(kmsa));
    }

    [Theory]
    [InlineData("timeZone", "Africa/Jinja")]
    [InlineData("defaultLocale", "en-XK")]
    [InlineData("currencyCode", "XAU")]
    [InlineData("startingSoonMinutes", 4)]
    [InlineData("startingSoonMinutes", 181)]
    public async Task Invalid_values_are_named(string field, object value)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, JsonSerializer.Serialize(new Dictionary<string, object> { [field] = value }));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        CreateTenantTests.Errors(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
        (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldBe(before);
    }

    [Theory]
    [InlineData("""[{"publicEnabled":true}]""")]
    [InlineData("null")]
    [InlineData("""{"publicEnabled":true""")]
    public async Task A_body_that_is_not_a_JSON_object_is_malformed(string body)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, body);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Each_tenant_sees_its_own_settings()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        using var change = await PatchAsync(elssc, _elsscCoordinatorId, """{"timeZone":"Africa/Nairobi","startingSoonMinutes":20}""");
        change.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldSatisfyAllConditions(
            s => s.TimeZone.ShouldBe("Africa/Kampala"),
            s => s.StartingSoonMinutes.ShouldBe(30));
        (await ReadAsync(elssc, _elsscCoordinatorId)).ShouldSatisfyAllConditions(
            s => s.TimeZone.ShouldBe("Africa/Nairobi"),
            s => s.StartingSoonMinutes.ShouldBe(20));
    }

    [Fact]
    public async Task Another_tenants_admin_cannot_read_the_settings()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);

        using var response = await GetAsync(kmsa, _elsscCoordinatorId, memberOf: elssc);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        problem.TryGetProperty("timeZone", out _).ShouldBeFalse();
        response.Headers.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task Another_tenants_admin_cannot_change_the_settings()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var before = await ReadAsync(kmsa, _kmsaSecretaryId);

        using var response = await PatchAsync(kmsa, _elsscCoordinatorId, """{"publicEnabled":true,"startingSoonMinutes":5}""", memberOf: elssc);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        problem.TryGetProperty("timeZone", out _).ShouldBeFalse();
        (await ReadAsync(kmsa, _kmsaSecretaryId)).ShouldBe(before);
        (await ReadAsync(elssc, _elsscCoordinatorId)).StartingSoonMinutes.ShouldBe(30, "nor are the caller's own settings changed");
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Archived)]
    public async Task A_blocked_tenant_cannot_read_or_change_its_settings(TenantStatus status)
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", status, Ct);

        using var read = await GetAsync(kmsa, _kmsaSecretaryId);
        using var change = await PatchAsync(kmsa, _kmsaSecretaryId, """{"startingSoonMinutes":60}""");

        await read.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        await change.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        (await TenantTestData.SettingsAsync(App.Services, kmsa, Ct)).StartingSoonMinutes.ShouldBe(30);
    }

    [Fact]
    public async Task A_provisioning_tenant_can_set_up_its_settings()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);

        using var response = await PatchAsync(kmsa, _kmsaSecretaryId, """{"minorRegistrationConsent":"REQUIRED"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TenantTestData.SettingsAsync(App.Services, kmsa, Ct)).MinorRegistrationConsent.ShouldBe(MinorRegistrationConsent.Required);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid tenantId, Guid userId, Guid? memberOf = null)
    {
        using var client = CreateClient(tenantId, userId, memberOf ?? tenantId);
        return await client.GetAsync(new Uri(SettingsPath, UriKind.Relative), Ct);
    }

    private async Task<TenantSettingsResponse> ReadAsync(Guid tenantId, Guid userId)
    {
        using var response = await GetAsync(tenantId, userId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TenantSettingsResponse>(Json, Ct)).ShouldNotBeNull();
    }

    private async Task<HttpResponseMessage> PatchAsync(Guid tenantId, Guid userId, string json, Guid? memberOf = null, string? ifMatch = null)
    {
        using var client = CreateClient(tenantId, userId, memberOf ?? tenantId);
        using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri(SettingsPath, UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request, Ct);
    }

    /// <summary>A member of <paramref name="memberOf"/> asking about <paramref name="tenantId"/>, allowed both actions.</summary>
    private HttpClient CreateClient(Guid tenantId, Guid userId, Guid memberOf)
    {
        App.Authorization.Allow(TenancyActions.TenantSettingsRead, TenancyActions.TenantSettingsUpdate);
        var client = App.CreateClient().AsUser(userId, memberOf);
        client.DefaultRequestHeaders.Add(_tenantHeader, tenantId.ToString());
        return client;
    }
}
