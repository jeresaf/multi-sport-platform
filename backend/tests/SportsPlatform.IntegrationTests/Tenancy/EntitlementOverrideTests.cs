using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// <c>/api/platform/v1/tenants/{tenantId}/entitlement-overrides</c> (S04 D5, D16): overrides over HTTP, under the route's
/// tenant, typed by their entitlement, audited with their reason, one in place per entitlement even under concurrency,
/// and never overwritten. Dates are relative to the tenant's today (Africa/Kampala).
/// </summary>
public sealed class EntitlementOverrideTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Pilot agreement: one extra seat for the fixtures secretary";

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    private DateOnly Today => TenantCalendar.Today(App.Clock, "Africa/Kampala");

    [Fact]
    public async Task Grant_adds_an_override_shown_in_the_subscription_and_audited_with_its_reason()
    {
        var tenantId = await TenantAsync("kmsa");

        using var response = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }, effectiveUntil: Today.AddDays(30)));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{TenantTestData.TenantsPath}/{tenantId}/subscription");
        var granted = (await ReadAsync(response)).Overrides.ShouldHaveSingleItem();
        granted.EntitlementCode.ShouldBe("MAX_STAFF_USERS");
        granted.Value.ShouldBe(EntitlementValue.Quota(5));
        granted.Reason.ShouldBe(_reason);
        granted.EffectiveUntil.ShouldBe(Today.AddDays(30));
        granted.InEffect.ShouldBeTrue();
        granted.GrantedBy.ShouldBe(TenantTestData.StaffId);
        (granted.RemovedAt, granted.RemovedBy, granted.RemoveReason).ShouldBe((null, null, null));

        using var read = await GetAsync(tenantId);
        (await ReadAsync(read)).Overrides.ShouldHaveSingleItem().Id.ShouldBe(granted.Id);
        (await RowsAsync(tenantId)).ShouldHaveSingleItem().ShouldBe((granted.Id, "MAX_STAFF_USERS", false, (string?)null));
        var entry = (await AuditEntriesAsync()).Single(e => e.ActionCode == "entitlement.override");
        entry.TenantId.ShouldBe(tenantId);
        entry.EntityType.ShouldBe(SubscriptionChanges.OverrideAuditEntityType);
        entry.EntityId.ShouldBe(granted.Id);
        entry.Reason.ShouldBe(_reason);
        var metadata = Metadata(entry);
        metadata.GetProperty("entitlementCode").GetString().ShouldBe("MAX_STAFF_USERS");
        metadata.GetProperty("value").GetProperty("limit").GetInt32().ShouldBe(5);
        metadata.GetProperty("replacedOverrideId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("CUSTOM_DOMAIN", """{ "enabled": true }""")]
    [InlineData("MAX_TEAMS", """{ "limit": null }""")]
    [InlineData("REPORTS", """{ "level": "ADVANCED" }""")]
    [InlineData("BRANDING", """{ "level": null }""")]
    public async Task Grant_takes_a_value_of_each_type(string code, string value)
    {
        var tenantId = await TenantAsync("kmsa");

        using var response = await GrantAsync(tenantId, Grant(code, JsonDocument.Parse(value).RootElement));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var granted = (await ReadAsync(response)).Overrides.ShouldHaveSingleItem();
        granted.EntitlementCode.ShouldBe(code);
        JsonSerializer.Serialize(granted.Value).ShouldBe(JsonSerializer.Serialize(JsonDocument.Parse(value).RootElement));
        granted.EffectiveUntil.ShouldBeNull();
    }

    // S04 D5: replacing one removes the old row and adds a new one in one save (history kept, AR-18).
    [Fact]
    public async Task A_second_grant_replaces_the_first_and_keeps_its_row()
    {
        var tenantId = await TenantAsync("kmsa");
        using var first = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }));
        var firstId = (await ReadAsync(first)).Overrides.Single().Id;

        using var second = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 8 }, reason: "Two more scorers for the festival"));

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var overrides = (await ReadAsync(second)).Overrides;
        overrides.Count.ShouldBe(2);
        var replacement = overrides[0];
        (replacement.Value, replacement.InEffect, replacement.RemovedAt).ShouldBe((EntitlementValue.Quota(8), true, null));
        var replaced = overrides[1];
        replaced.Id.ShouldBe(firstId);
        (replaced.Value, replaced.InEffect, replaced.RemoveReason).ShouldBe((EntitlementValue.Quota(5), false, TenantEntitlementOverrides.ReplacedReason));
        replaced.RemovedBy.ShouldBe(TenantTestData.StaffId);

        (await RowsAsync(tenantId)).ShouldBe(
            [(firstId, "MAX_STAFF_USERS", true, TenantEntitlementOverrides.ReplacedReason), (replacement.Id, "MAX_STAFF_USERS", false, null)],
            ignoreOrder: true);
        var entry = (await AuditEntriesAsync()).Single(e => e.ActionCode == "entitlement.override" && e.EntityId == replacement.Id);
        Metadata(entry).GetProperty("replacedOverrideId").GetGuid().ShouldBe(firstId);
    }

    [Fact]
    public async Task Concurrent_grants_of_one_entitlement_leave_exactly_one_in_place()
    {
        var tenantId = await TenantAsync("kmsa");

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(i => GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = i }))));

        try
        {
            responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
            var rows = await RowsAsync(tenantId);
            rows.Count.ShouldBe(10);
            rows.Count(r => !r.Removed).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Overrides_of_other_entitlements_stay_in_place()
    {
        var tenantId = await TenantAsync("elssc");
        using var editions = await GrantAsync(tenantId, Grant("MAX_ACTIVE_EDITIONS", new { limit = 1 }, effectiveUntil: Today.AddDays(44)));

        using var teams = await GrantAsync(tenantId, Grant("MAX_TEAMS", new { limit = 24 }));

        (await ReadAsync(teams)).Overrides.Select(o => (o.EntitlementCode, o.InEffect))
            .ShouldBe([("MAX_ACTIVE_EDITIONS", true), ("MAX_TEAMS", true)], ignoreOrder: true);
    }

    [Theory]
    [InlineData("entitlementCode", "MAX_GOALS", "TENANT_OVERRIDE_INVALID_VALUE")]
    [InlineData("entitlementCode", null, "TENANT_OVERRIDE_INVALID_VALUE")]
    [InlineData("value", """{ "enabled": true }""", "TENANT_OVERRIDE_INVALID_VALUE")]
    [InlineData("value", """{ "level": "BASIC" }""", "TENANT_OVERRIDE_INVALID_VALUE")]
    [InlineData("value", null, "VALIDATION_INVALID_REQUEST")]
    [InlineData("reason", " ", "VALIDATION_REASON_REQUIRED")]
    [InlineData("reason", null, "VALIDATION_REASON_REQUIRED")]
    public async Task Grant_names_the_field_that_is_not_valid(string field, string? value, string code)
    {
        var tenantId = await TenantAsync("kmsa");
        var body = Grant("MAX_STAFF_USERS", new { limit = 5 });
        body[field] = field == "value" && value is not null ? JsonDocument.Parse(value).RootElement : value;

        using var response = await GrantAsync(tenantId, body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("code").GetString().ShouldBe(code);
        (await RowsAsync(tenantId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Grant_refuses_a_level_outside_the_entitlements_levels_on_the_value()
    {
        var tenantId = await TenantAsync("kmsa");

        using var response = await GrantAsync(tenantId, Grant("REPORTS", new { level = "FULL" }));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()).ShouldBe(("value", "TENANT_OVERRIDE_INVALID_VALUE"));
    }

    [Theory]
    [InlineData("""{ "limit": -1 }""")]
    [InlineData("""{ "limit": 5, "enabled": true }""")]
    [InlineData("5")]
    public async Task Grant_refuses_a_value_that_is_not_an_entitlement_value_as_malformed(string value)
    {
        var tenantId = await TenantAsync("kmsa");
        var body = Grant("MAX_STAFF_USERS", JsonDocument.Parse(value).RootElement);

        using var response = await GrantAsync(tenantId, body);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task An_override_ending_today_is_in_effect_until_midnight_in_the_tenants_time_zone()
    {
        var tenantId = await TenantAsync("kmsa");
        using var yesterday = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }, effectiveUntil: Today.AddDays(-1)));
        using var today = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }, effectiveUntil: Today));

        var problem = await yesterday.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem().GetProperty("field").GetString().ShouldBe("effectiveUntil");
        (await ReadAsync(today)).Overrides.ShouldHaveSingleItem().InEffect.ShouldBeTrue();

        // Midnight in Kampala (UTC+3) is 21:00 UTC.
        var now = App.Clock.GetUtcNow();
        App.Clock.Advance(new DateTimeOffset(now.Year, now.Month, now.Day, 21, 0, 0, TimeSpan.Zero).AddDays(now.Hour >= 21 ? 1 : 0) - now);
        using var read = await GetAsync(tenantId);
        (await ReadAsync(read)).Overrides.ShouldHaveSingleItem().InEffect.ShouldBeFalse();
    }

    [Fact]
    public async Task Remove_keeps_the_row_with_the_reason_and_is_audited()
    {
        var tenantId = await TenantAsync("kmsa");
        using var granted = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }));
        var overrideId = (await ReadAsync(granted)).Overrides.Single().Id;

        using var response = await RemoveAsync(tenantId, overrideId, "Pilot ended");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var removed = (await ReadAsync(response)).Overrides.ShouldHaveSingleItem();
        (removed.Id, removed.InEffect, removed.RemoveReason, removed.RemovedBy).ShouldBe((overrideId, false, "Pilot ended", TenantTestData.StaffId));
        removed.RemovedAt.ShouldNotBeNull();
        (await RowsAsync(tenantId)).ShouldHaveSingleItem().ShouldBe((overrideId, "MAX_STAFF_USERS", true, "Pilot ended"));
        var entry = (await AuditEntriesAsync()).Single(e => e.ActionCode == "entitlement.override.remove");
        (entry.TenantId, entry.EntityType, entry.EntityId, entry.Reason, entry.Outcome)
            .ShouldBe((tenantId, SubscriptionChanges.OverrideAuditEntityType, overrideId, "Pilot ended", AuditOutcome.Success));
        Metadata(entry).GetProperty("entitlementCode").GetString().ShouldBe("MAX_STAFF_USERS");
    }

    [Fact]
    public async Task Removing_an_override_twice_another_tenants_or_an_unknown_one_is_not_found()
    {
        var kmsa = await TenantAsync("kmsa");
        var elssc = await TenantAsync("elssc");
        using var granted = await GrantAsync(elssc, Grant("MAX_ACTIVE_EDITIONS", new { limit = 1 }));
        var elsscOverride = (await ReadAsync(granted)).Overrides.Single().Id;
        using var removed = await RemoveAsync(elssc, elsscOverride, "Event pass cancelled");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var twice = await RemoveAsync(elssc, elsscOverride, "Again");
        using var otherTenant = await RemoveAsync(kmsa, elsscOverride, "Wrong tenant");
        using var unknown = await RemoveAsync(kmsa, Guid.CreateVersion7(), "Unknown");

        await twice.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_OVERRIDE_NOT_FOUND");
        await otherTenant.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_OVERRIDE_NOT_FOUND");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_OVERRIDE_NOT_FOUND");
        (await RowsAsync(elssc)).ShouldHaveSingleItem().RemoveReason.ShouldBe("Event pass cancelled");
    }

    [Fact]
    public async Task Remove_needs_a_reason()
    {
        var tenantId = await TenantAsync("kmsa");
        using var granted = await GrantAsync(tenantId, Grant("MAX_STAFF_USERS", new { limit = 5 }));
        var overrideId = (await ReadAsync(granted)).Overrides.Single().Id;

        using var response = await RemoveAsync(tenantId, overrideId, " ");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        (await RowsAsync(tenantId)).ShouldHaveSingleItem().Removed.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found()
    {
        var unknown = Guid.CreateVersion7();

        using var grant = await GrantAsync(unknown, Grant("MAX_STAFF_USERS", new { limit = 5 }));
        using var remove = await RemoveAsync(unknown, Guid.CreateVersion7(), "Pilot ended");

        await grant.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
        await remove.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task A_tenant_reads_only_its_own_overrides()
    {
        var kmsa = await TenantAsync("kmsa");
        var elssc = await TenantAsync("elssc");
        using var granted = await GrantAsync(elssc, Grant("MAX_ACTIVE_EDITIONS", new { limit = 1 }));
        granted.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var read = await GetAsync(kmsa);

        (await ReadAsync(read)).Overrides.ShouldBeEmpty();
    }

    private static Dictionary<string, object?> Grant(string code, object value, string reason = _reason, DateOnly? effectiveUntil = null)
    {
        var body = new Dictionary<string, object?> { ["entitlementCode"] = code, ["value"] = value, ["reason"] = reason };
        if (effectiveUntil is { } until)
        {
            body["effectiveUntil"] = until.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return body;
    }

    private Task<Guid> TenantAsync(string slug) => TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);

    private static async Task<SubscriptionResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(Json, Ct)).ShouldNotBeNull();

    private async Task<HttpResponseMessage> GrantAsync(Guid tenantId, Dictionary<string, object?> body)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);
        return await client.PostAsJsonAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/entitlement-overrides", UriKind.Relative), body, Ct);
    }

    private async Task<HttpResponseMessage> RemoveAsync(Guid tenantId, Guid overrideId, string reason)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);
        return await client.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{tenantId}/entitlement-overrides/{overrideId}/remove", UriKind.Relative),
            new { reason },
            Ct);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid tenantId)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);
        return await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/subscription", UriKind.Relative), Ct);
    }

    private static JsonElement Metadata(AuditEntry entry) =>
        JsonDocument.Parse(entry.Metadata.ShouldNotBeNull()).RootElement.Clone();

    /// <summary>The audit entries, after delivering the outbox: a command's entry is an outbox message until then.</summary>
    private async Task<List<AuditEntry>> AuditEntriesAsync()
    {
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }

    /// <summary>The tenant's overrides as the platform role reads them (every tenant, no RLS): id, code, removed, why.</summary>
    private Task<List<(Guid Id, string EntitlementCode, bool Removed, string? RemoveReason)>> RowsAsync(Guid tenantId) => QueryAsync(
        DatabaseRoles.Platform,
        "SELECT id, entitlement_code, removed_at IS NOT NULL, remove_reason FROM tenancy.tenant_entitlement_overrides WHERE tenant_id = @tenant",
        reader => (reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3)),
        new NpgsqlParameter("tenant", tenantId));
}
