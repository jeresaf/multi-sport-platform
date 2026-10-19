using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary><c>PATCH /api/platform/v1/tenants/{tenantId}</c> (<c>tenant.update</c>).</summary>
public sealed class UpdateTenantProfileTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task Changes_only_the_fields_sent()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await GetAsync(tenantId);
        App.Clock.Advance(TimeSpan.FromHours(2));

        using var response = await PatchAsync(tenantId, new { displayName = "  KMSA  ", defaultLocale = "lg-UG", publicEnabled = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
        after.ShouldBe(before with
        {
            DisplayName = "KMSA",
            DefaultLocale = "lg-UG",
            PublicEnabled = true,
            UpdatedAt = App.Clock.GetUtcNow(),
            Version = after.Version,
        });
        after.Version.ShouldNotBe(before.Version);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{after.Version}\"");
        (await GetAsync(tenantId)).ShouldBe(after);
        (await TenantTestData.HistoryAsync(App.Services, tenantId, Ct)).Count.ShouldBe(2, "a profile change is not a status change");
    }

    [Fact]
    public async Task Is_audited_under_the_tenant_with_the_names_of_the_fields_sent()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PatchAsync(tenantId, new { legalName = "Kampala Metro Sports Association Ltd", currencyCode = "UGX" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await App.Services.GetRequiredService<OutboxProcessor>().ProcessOnceAsync(Ct);

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.update"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(tenantId),
            e => e.EntityId.ShouldBe(tenantId));
        JsonDocument.Parse(entry.Metadata.ShouldNotBeNull()).RootElement.GetProperty("fields").EnumerateArray()
            .Select(f => f.GetString()).ShouldBe(["legalName", "currencyCode"]);
    }

    [Fact]
    public async Task A_change_that_changes_nothing_saves_nothing()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var before = await GetAsync(tenantId);

        using var response = await PatchAsync(tenantId, new { countryCode = before.CountryCode, version = before.Version });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldBe(before);
    }

    [Fact]
    public async Task The_current_version_is_accepted_and_a_stale_one_conflicts()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var read = await GetAsync(tenantId);

        using var first = await PatchAsync(tenantId, new { displayName = "KMSA", version = read.Version });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await PatchAsync(tenantId, new { displayName = "Kampala Metro", version = read.Version });
        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_CONCURRENCY_CONFLICT");
        (await GetAsync(tenantId)).DisplayName.ShouldBe("KMSA");
    }

    [Theory]
    [InlineData(0u, HttpStatusCode.OK)]
    [InlineData(1u, HttpStatusCode.Conflict)]
    public async Task If_match_is_evaluated(uint offset, HttpStatusCode expected)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var read = await GetAsync(tenantId);
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { displayName = "KMSA" }),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{read.Version + offset}\""));

        using var response = await client.SendAsync(request, Ct);

        if (expected == HttpStatusCode.OK)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await GetAsync(tenantId)).DisplayName.ShouldBe("KMSA");
        }
        else
        {
            await response.ShouldBeProblemAsync(expected, "TENANT_CONCURRENCY_CONFLICT");
            (await GetAsync(tenantId)).ShouldBe(read);
        }
    }

    [Fact]
    public async Task Unknown_tenant_is_not_found()
    {
        using var response = await PatchAsync(new Guid("0192a1b2-0000-7000-8000-00000000c404"), new { displayName = "KMSA" });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
    }

    [Theory]
    [InlineData("countryCode", "XK")]
    [InlineData("timeZone", "Africa/Jinja")]
    [InlineData("defaultLocale", "en-XK")]
    [InlineData("currencyCode", "XAU")]
    [InlineData("legalName", "")]
    public async Task Invalid_fields_are_named(string field, string value)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PatchAsync(tenantId, new Dictionary<string, object?> { [field] = value });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        CreateTenantTests.Errors(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
    }

    [Theory]
    [InlineData("slug", "kampala-metro")]
    [InlineData("status", "ARCHIVED")]
    public async Task Slug_and_status_cannot_be_changed_here(string field, string value)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PatchAsync(tenantId, new Dictionary<string, object?> { [field] = value });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        var tenant = await GetAsync(tenantId);
        tenant.Slug.ShouldBe("kmsa");
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    private async Task<HttpResponseMessage> PatchAsync(Guid tenantId, object body)
    {
        using var client = CreateClient();
        return await client.PatchAsJsonAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative), body, Ct);
    }

    private async Task<TenantResponse> GetAsync(Guid tenantId)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
    }

    /// <summary>A platform staff member allowed this endpoint's actions (the base class resets both before each test).</summary>
    private HttpClient CreateClient()
    {
        App.Authorization.Allow(TenancyActions.TenantRead, TenancyActions.TenantUpdate);
        return App.CreateClient().AsUser(TenantTestData.StaffId);
    }
}
