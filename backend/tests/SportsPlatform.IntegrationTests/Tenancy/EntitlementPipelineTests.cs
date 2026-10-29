using System.Net;
using System.Net.Http.Json;
using System.Text;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Pack spec 07 §30 steps 1–10 and 13–16 over HTTP (S04 D10, D20), with the test-only boolean entitlement
/// <c>FIXTURE_PUBLISHING</c> (off on STANDARD, on on PRO) and the test action <c>fixture.publish</c> that names it:
/// step 2 refuses what the plan or an override doesn't give, after step 1 and before the evaluator; overrides, expiry,
/// renewal and a time-zone change count from the next request (the cache is cleared, D7). Quota steps 11–12 are
/// in <c>Identity/StaffUserQuotaTests</c> (<c>MAX_STAFF_USERS</c>). Dates are the tenant's (Africa/Kampala, UTC+3).
/// </summary>
public sealed class EntitlementPipelineTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _settingsPath = "/api/admin/v1/tenant/settings";
    private const string _reason = "Pilot agreement: fixture publishing for the 2026 season";

    // 2026-10-17 12:00 in Kampala.
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _today = new(2026, 10, 17);
    private static readonly Guid _secretaryId = new("0192a1b2-0000-7000-8000-00000000c301");

    // §30 1–7: two plans give the entitlement different values, so one action passes for one tenant only; with the
    // permission but not the entitlement, step 2 refuses before the evaluator is asked.
    [Fact]
    public async Task Two_plans_with_different_values_decide_the_same_action()
    {
        await RunAsync(async app =>
        {
            var kmsa = await TenantTestData.SaveTenantAsync(app.Services, "kmsa", TenantStatus.Active, Ct);
            var elssc = await TenantOnProAsync(app, "elssc", _today.AddDays(-16), _today.AddDays(348));
            app.Authorization.Allow(EntitlementTestApp.FixturePublish);

            var refused = await (await PublishAsync(app, kmsa)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_REQUIRED");
            refused.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("FIXTURE_PUBLISHING");
            app.Authorization.Requests.ShouldNotContain(request => request.Action == EntitlementTestApp.FixturePublish);
            (await PublishAsync(app, elssc)).StatusCode.ShouldBe(HttpStatusCode.OK);
            app.Authorization.Requests.ShouldContain(request => request.Action == EntitlementTestApp.FixturePublish);

            // The platform read shows each tenant's value with its source.
            (await EntitlementAsync(app, kmsa)).ShouldBe(new EffectiveEntitlementResponse(
                "FIXTURE_PUBLISHING", EntitlementValue.Flag(false), EntitlementSource.Plan));
            (await EntitlementAsync(app, elssc)).ShouldBe(new EffectiveEntitlementResponse(
                "FIXTURE_PUBLISHING", EntitlementValue.Flag(true), EntitlementSource.Plan));
            var denied = (await AuditEntriesAsync(app)).Single(e => e.ActionCode == "fixture.publish" && e.TenantId == kmsa);
            (denied.Outcome, denied.ErrorCode).ShouldBe((AuditOutcome.Denied, "ENTITLEMENT_REQUIRED"));
        });
    }

    // §30 8: the entitlement without the permission is refused by step 3.
    [Fact]
    public async Task The_entitlement_without_the_permission_is_refused()
    {
        await RunAsync(async app =>
        {
            var elssc = await TenantOnProAsync(app, "elssc", _today.AddDays(-16), _today.AddDays(348));

            await (await PublishAsync(app, elssc)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        });
    }

    // §30 9–10: an override gives the entitlement from the next request and is audited with its reason; removing it
    // takes it away again.
    [Fact]
    public async Task An_override_applies_from_the_next_request_and_is_audited()
    {
        await RunAsync(async app =>
        {
            var kmsa = await TenantTestData.SaveTenantAsync(app.Services, "kmsa", TenantStatus.Active, Ct);
            app.Authorization.Allow(EntitlementTestApp.FixturePublish);
            await (await PublishAsync(app, kmsa)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_REQUIRED");

            var granted = await GrantAsync(app, kmsa, effectiveUntil: null);

            (await PublishAsync(app, kmsa)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await EntitlementAsync(app, kmsa)).ShouldBe(new EffectiveEntitlementResponse(
                "FIXTURE_PUBLISHING", EntitlementValue.Flag(true), EntitlementSource.Override));
            var entry = (await AuditEntriesAsync(app)).Single(e => e.ActionCode == "entitlement.override");
            (entry.TenantId, entry.EntityType, entry.EntityId, entry.Reason, entry.Outcome)
                .ShouldBe((kmsa, SubscriptionChanges.OverrideAuditEntityType, granted, _reason, AuditOutcome.Success));

            app.Authorization.Allow(TenancyActions.EntitlementOverrideRemove);
            using var platform = PlatformClient(app);
            (await platform.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{kmsa}/entitlement-overrides/{granted}/remove", UriKind.Relative),
                new { reason = "Season ended" },
                Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            await (await PublishAsync(app, kmsa)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_REQUIRED");
        });
    }

    // "Today" is derived on every request, never cached: an override ends after its last day even while the tenant's
    // inputs are cached.
    [Fact]
    public async Task An_override_stops_applying_after_its_last_day()
    {
        await RunAsync(async app =>
        {
            var kmsa = await TenantTestData.SaveTenantAsync(app.Services, "kmsa", TenantStatus.Active, Ct);
            app.Authorization.Allow(EntitlementTestApp.FixturePublish);
            await GrantAsync(app, kmsa, effectiveUntil: _today);
            (await PublishAsync(app, kmsa)).StatusCode.ShouldBe(HttpStatusCode.OK);

            // Midnight in Kampala: 21:00 UTC.
            app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 17, 21, 0, 0, TimeSpan.Zero));

            await (await PublishAsync(app, kmsa)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_REQUIRED");
        });
    }

    // §30 13–16: an expired PRO subscription makes the tenant read-only, which step 1 refuses before step 2 (the plan
    // still gives the entitlement); renewing restores the action on the next request.
    [Fact]
    public async Task Expiry_makes_the_tenant_read_only_and_renewal_restores_the_action()
    {
        await RunAsync(async app =>
        {
            var elssc = await TenantOnProAsync(app, "elssc", _today.AddDays(-16), _today.AddDays(3), graceDays: 2);
            app.Authorization.Allow(EntitlementTestApp.FixturePublish);
            (await PublishAsync(app, elssc)).StatusCode.ShouldBe(HttpStatusCode.OK);

            // The last day of grace was 2026-10-22; 2026-10-23 in Kampala.
            app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero));
            await (await PublishAsync(app, elssc)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
            (await EntitlementAsync(app, elssc)).Value.ShouldBe(EntitlementValue.Flag(true));

            app.Authorization.Allow(TenancyActions.SubscriptionRenew);
            using var platform = PlatformClient(app);
            (await platform.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{elssc}/subscription/renew", UriKind.Relative),
                new { endsOn = new DateOnly(2027, 10, 22) },
                Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            (await PublishAsync(app, elssc)).StatusCode.ShouldBe(HttpStatusCode.OK);
        });
    }

    // The time zone decides the tenant's date, so a settings change clears the cached inputs too (owner default, plan
    // 2026-10-23): at 02:00 UTC on 2026-10-19 it is the 19th in Kampala (the period has started) and still the 18th in
    // Los Angeles (it hasn't, so READ_ONLY).
    [Fact]
    public async Task A_time_zone_change_counts_from_the_next_request()
    {
        await RunAsync(async app =>
        {
            var periodStartsOn = new DateOnly(2026, 10, 19);
            app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero));
            var elssc = await TenantOnProAsync(app, "elssc", periodStartsOn, periodStartsOn.AddYears(1).AddDays(-1));
            app.Authorization.Allow(EntitlementTestApp.FixturePublish, TenancyActions.TenantSettingsUpdate);
            (await PublishAsync(app, elssc)).StatusCode.ShouldBe(HttpStatusCode.OK);

            using var member = MemberClient(app, elssc);
            (await member.PatchAsync(
                _settingsPath, new StringContent("""{"timeZone":"America/Los_Angeles"}""", Encoding.UTF8, "application/json"), Ct))
                .StatusCode.ShouldBe(HttpStatusCode.OK);

            await (await PublishAsync(app, elssc)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        });
    }

    // The platform's tenant update can change the time zone too, and clears the cached inputs the same way (#68 R1).
    [Fact]
    public async Task A_time_zone_change_by_the_platform_counts_from_the_next_request()
    {
        await RunAsync(async app =>
        {
            var periodStartsOn = new DateOnly(2026, 10, 19);
            app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero));
            var elssc = await TenantOnProAsync(app, "elssc", periodStartsOn, periodStartsOn.AddYears(1).AddDays(-1));
            app.Authorization.Allow(EntitlementTestApp.FixturePublish, TenancyActions.TenantUpdate);
            (await PublishAsync(app, elssc)).StatusCode.ShouldBe(HttpStatusCode.OK);

            using var platform = PlatformClient(app);
            (await platform.PatchAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{elssc}", UriKind.Relative), new { timeZone = "America/Los_Angeles" }, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.OK);

            await (await PublishAsync(app, elssc)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        });
    }

    /// <summary>
    /// Writes the test entitlement's plan values, starts the app on them (its plan values are cached until restart),
    /// runs <paramref name="test"/> and removes the values: the plan tables outlive the reset (PostgresFixture).
    /// </summary>
    private async Task RunAsync(Func<EntitlementTestApp, Task> test)
    {
        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, EntitlementTestApp.SavePlanValuesSql);
            await using var app = await EntitlementTestApp.StartAsync(Fixture, _now, Ct);
            await test(app);
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, EntitlementTestApp.RemovePlanValuesSql);
        }
    }

    /// <summary>An active tenant without test data's period, given a PRO period through <c>subscription.assign</c>.</summary>
    private static async Task<Guid> TenantOnProAsync(EntitlementTestApp app, string slug, DateOnly startsOn, DateOnly endsOn, int graceDays = 14)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(
            app.Services, slug, TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        app.Authorization.Allow(TenancyActions.SubscriptionAssign);
        using var platform = PlatformClient(app);
        using var response = await platform.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{tenantId}/subscription/periods", UriKind.Relative),
            new { planCode = "PRO", kind = "PAID", startsOn, endsOn, graceDays },
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return tenantId;
    }

    /// <summary>Grants <c>FIXTURE_PUBLISHING</c> on through <c>entitlement.override</c>; returns the override's ID.</summary>
    private static async Task<Guid> GrantAsync(EntitlementTestApp app, Guid tenantId, DateOnly? effectiveUntil)
    {
        app.Authorization.Allow(TenancyActions.EntitlementOverride);
        using var platform = PlatformClient(app);
        using var response = await platform.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{tenantId}/entitlement-overrides", UriKind.Relative),
            new { entitlementCode = "FIXTURE_PUBLISHING", value = new { enabled = true }, reason = _reason, effectiveUntil },
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadAsync(response)).Overrides.Single(o => o.RemovedAt is null).Id;
    }

    private static async Task<EffectiveEntitlementResponse> EntitlementAsync(EntitlementTestApp app, Guid tenantId)
    {
        app.Authorization.Allow(TenancyActions.SubscriptionRead);
        using var platform = PlatformClient(app);
        using var response = await platform.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/subscription", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsync(response)).Entitlements.Single(e => e.Code == "FIXTURE_PUBLISHING");
    }

    private static async Task<HttpResponseMessage> PublishAsync(EntitlementTestApp app, Guid tenantId)
    {
        using var member = MemberClient(app, tenantId);
        return await member.PostAsync(EntitlementTestApp.PublishPath, content: null, Ct);
    }

    private static HttpClient PlatformClient(EntitlementTestApp app) => app.CreateClient().AsUser(TenantTestData.StaffId);

    /// <summary>The competitions secretary, a member of <paramref name="tenantId"/> by the test header.</summary>
    private static HttpClient MemberClient(EntitlementTestApp app, Guid tenantId)
    {
        var client = app.CreateClient().AsUser(_secretaryId, tenantId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static async Task<SubscriptionResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(MessageSerialization.Options, Ct)).ShouldNotBeNull();

    /// <summary>The audit entries, after delivering the outbox: a command's entry is an outbox message until then.</summary>
    private static async Task<List<AuditEntry>> AuditEntriesAsync(EntitlementTestApp app)
    {
        await AccountTestData.DeliverOutboxAsync(app.Services, Ct);
        return await AuditTestData.EntriesAsync(app.Services, Ct);
    }
}
