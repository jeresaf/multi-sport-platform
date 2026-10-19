using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary><c>POST /api/platform/v1/tenants</c> (<c>tenant.create</c>).</summary>
public sealed class CreateTenantTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static JsonSerializerOptions Json => MessageSerialization.Options;

    [Fact]
    public async Task Creates_a_provisioning_tenant_with_its_location_and_version()
    {
        using var response = await CreateAsync(TenantTestData.CreateBody());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tenant = (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
        tenant.ShouldSatisfyAllConditions(
            t => t.Slug.ShouldBe("kmsa"),
            t => t.LegalName.ShouldBe("Kampala Metro Sports Association Limited"),
            t => t.DisplayName.ShouldBe("Kampala Metro Sports Association"),
            t => t.CountryCode.ShouldBe("UG"),
            t => t.TimeZone.ShouldBe("Africa/Kampala"),
            t => t.DefaultLocale.ShouldBe("en-UG"),
            t => t.CurrencyCode.ShouldBe("UGX"),
            t => t.Status.ShouldBe(TenantStatus.Provisioning),
            t => t.PublicEnabled.ShouldBeFalse(),
            t => t.CreatedAt.ShouldBe(App.Clock.GetUtcNow()),
            t => t.Version.ShouldNotBe(0u));
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{TenantTestData.TenantsPath}/{tenant.Id}");
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{tenant.Version}\"");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"status\":\"PROVISIONING\"");
    }

    [Fact]
    public async Task Writes_the_first_history_row_the_event_and_the_audit_entry_under_the_new_tenant()
    {
        using var response = await CreateAsync(TenantTestData.CreateBody(
            "elssc", ("legalName", "Eastern Lakes Schools Sports Council"), ("publicEnabled", true)));
        var tenant = (await response.Content.ReadFromJsonAsync<TenantResponse>(Json, Ct)).ShouldNotBeNull();
        tenant.PublicEnabled.ShouldBeTrue();

        var row = (await TenantTestData.HistoryAsync(App.Services, tenant.Id, Ct)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(
            r => r.FromStatus.ShouldBeNull(),
            r => r.ToStatus.ShouldBe(TenantStatus.Provisioning),
            r => r.Reason.ShouldBeNull(),
            r => r.ActorKind.ShouldBe(ActorKind.User),
            r => r.ActorId.ShouldBe(TenantTestData.StaffId),
            r => r.OccurredAt.ShouldBe(App.Clock.GetUtcNow()));

        var messages = await TenantTestData.OutboxAsync(App.Services, Ct);
        messages.Select(m => m.EventName).ShouldBe(["TenantCreated", "AuditRecorded"], ignoreOrder: true);
        messages.ShouldAllBe(m => m.TenantId == tenant.Id);
        var created = messages.Single(m => m.EventName == "TenantCreated");
        created.ShouldSatisfyAllConditions(
            m => m.EventVersion.ShouldBe(1),
            m => m.AggregateType.ShouldBe("Tenant"),
            m => m.AggregateId.ShouldBe(tenant.Id));
        JsonSerializer.Deserialize<TenantCreated.V1>(created.Payload, Json)
            .ShouldBe(new TenantCreated.V1(tenant.Id, "elssc", TenantStatus.Provisioning));

        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("tenant.create"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.TenantId.ShouldBe(tenant.Id),
            e => e.EntityType.ShouldBe("Tenant"),
            e => e.EntityId.ShouldBe(tenant.Id),
            e => e.ActorId.ShouldBe(TenantTestData.StaffId));
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Archived)]
    public async Task A_slug_is_never_reused(TenantStatus holderStatus)
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", holderStatus, Ct);

        using var response = await CreateAsync(TenantTestData.CreateBody("kmsa"));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SLUG_TAKEN");
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(1);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Failure),
            e => e.ErrorCode.ShouldBe("TENANT_SLUG_TAKEN"),
            e => e.TenantId.ShouldBeNull("no tenant was created (#30 R2)"),
            e => e.EntityId.ShouldBeNull());
    }

    [Theory]
    [InlineData("slug", "www")]
    [InlineData("slug", "Kmsa")]
    [InlineData("slug", "-kmsa")]
    [InlineData("slug", "km")]
    [InlineData("legalName", "   ")]
    [InlineData("displayName", "Kampala Metro Sports Association — the association of clubs, schools and leagues of greater Kampala and Wakiso District")]
    [InlineData("countryCode", "XK")]
    [InlineData("countryCode", "ug")]
    [InlineData("timeZone", "Africa/Jinja")]
    [InlineData("defaultLocale", "en_UG")]
    [InlineData("defaultLocale", "xx")]
    [InlineData("defaultLocale", "en-XK")]
    [InlineData("currencyCode", "XAU")]
    [InlineData("currencyCode", "ugx")]
    public async Task Invalid_fields_are_named(string field, string value)
    {
        using var response = await CreateAsync(TenantTestData.CreateBody(changes: (field, value)));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Errors(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Every_missing_field_is_named()
    {
        using var response = await CreateAsync(new Dictionary<string, object?> { ["publicEnabled"] = true });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Errors(problem).Select(e => e.Field).ShouldBe(
            ["slug", "legalName", "displayName", "countryCode", "timeZone", "defaultLocale", "currencyCode"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("status", "ACTIVE")]
    [InlineData("id", "0192a1b2-0000-7000-8000-00000000c001")]
    public async Task Members_that_are_not_in_the_request_are_rejected(string field, string value)
    {
        using var response = await CreateAsync(TenantTestData.CreateBody(changes: (field, value)));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(0);
    }

    private async Task<HttpResponseMessage> CreateAsync(object body)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), body, Ct);
    }

    internal static List<(string Field, string Code)> Errors(JsonElement problem) =>
    [
        .. problem.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("field").GetString()!, e.GetProperty("code").GetString()!)),
    ];

    /// <summary>A platform staff member allowed this endpoint's actions (the base class resets both before each test).</summary>
    private HttpClient CreateClient()
    {
        App.Authorization.Allow(TenancyActions.TenantCreate);
        return App.CreateClient().AsUser(TenantTestData.StaffId);
    }
}
