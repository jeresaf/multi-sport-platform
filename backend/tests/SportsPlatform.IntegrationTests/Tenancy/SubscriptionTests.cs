using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// <c>/api/platform/v1/tenants/{tenantId}/subscription</c> (S04 D3, D4, D16): periods over HTTP, under the route's
/// tenant, audited, never overwritten, and never overlapping, even under concurrency. The app's clock only moves
/// forward, so dates are relative to the tenant's today (Africa/Kampala, UTC+3).
/// </summary>
public sealed class SubscriptionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Association stopped running competitions";

    private static JsonSerializerOptions Json => MessageSerialization.Options;

    private DateOnly Today => TenantCalendar.Today(App.Clock, "Africa/Kampala");

    [Fact]
    public async Task Assign_adds_an_active_period_with_the_default_windows_and_is_audited_under_the_tenant()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-10), Today.AddDays(170), kind: "COMPLIMENTARY"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{TenantTestData.TenantsPath}/{tenantId}/subscription");
        var subscription = await ReadAsync(response);
        subscription.TenantId.ShouldBe(tenantId);
        subscription.Today.ShouldBe(Today);
        subscription.TimeZone.ShouldBe("Africa/Kampala");
        var period = subscription.Periods.ShouldHaveSingleItem();
        subscription.CurrentPeriodId.ShouldBe(period.Id);
        subscription.NextPeriodId.ShouldBeNull();
        period.PlanCode.ShouldBe("STANDARD");
        period.Kind.ShouldBe(SubscriptionKind.Complimentary);
        period.Status.ShouldBe(SubscriptionStatus.Active);
        (period.ExpiringWindowDays, period.GraceDays).ShouldBe((30, 14));
        period.ExpiringFrom.ShouldBe(Today.AddDays(141));
        period.GraceEndsOn.ShouldBe(Today.AddDays(184));
        period.CreatedBy.ShouldBe(TenantTestData.StaffId);

        (await RowsAsync(tenantId)).ShouldHaveSingleItem().ShouldBe((period.Id, "STANDARD", false, (string?)null));
        var entry = (await AuditEntriesAsync()).ShouldHaveSingleItem();
        entry.ActionCode.ShouldBe("subscription.assign");
        entry.TenantId.ShouldBe(tenantId);
        entry.EntityType.ShouldBe(SubscriptionChanges.AuditEntityType);
        entry.EntityId.ShouldBe(period.Id);
    }

    [Fact]
    public async Task Assign_refuses_a_period_that_overlaps_one_that_is_not_cancelled()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var first = await PostAsync(tenantId, "periods", Assign(Today, Today.AddDays(179)));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var overlapping = await PostAsync(tenantId, "periods", Assign(Today.AddDays(179), Today.AddDays(364)));
        using var next = await PostAsync(tenantId, "periods", Assign(Today.AddDays(180), Today.AddDays(364)));

        await overlapping.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_OVERLAP");
        next.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RowsAsync(tenantId)).Count.ShouldBe(2);
    }

    // S04-T2 acceptance: overlapping periods are refused under concurrency.
    [Fact]
    public async Task Concurrent_assigns_of_overlapping_periods_add_exactly_one()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            PostAsync(tenantId, "periods", Assign(Today.AddDays(i), Today.AddDays(180 + i)))));

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            {
                await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_OVERLAP");
            }

            (await RowsAsync(tenantId)).ShouldHaveSingleItem();
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("planCode", "GOLD", "TENANT_PLAN_NOT_FOUND")]
    [InlineData("planCode", null, "VALIDATION_INVALID_REQUEST")]
    [InlineData("kind", null, "VALIDATION_INVALID_REQUEST")]
    [InlineData("startsOn", null, "VALIDATION_INVALID_REQUEST")]
    [InlineData("endsOn", "2026-01-01", "VALIDATION_INVALID_REQUEST")]
    [InlineData("expiringWindowDays", 366, "VALIDATION_INVALID_REQUEST")]
    [InlineData("graceDays", -1, "VALIDATION_INVALID_REQUEST")]
    public async Task Assign_names_the_field_that_is_not_valid(string field, object? value, string code)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var body = Assign(Today, Today.AddDays(179));
        body[field] = value;

        using var response = await PostAsync(tenantId, "periods", body);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("code").GetString().ShouldBe(code);
        (await RowsAsync(tenantId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Assign_refuses_an_unknown_kind_or_field_as_malformed()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var unknownKind = Assign(Today, Today.AddDays(179), kind: "EVENT_PASS");
        var unknownField = Assign(Today, Today.AddDays(179));
        unknownField["status"] = "ACTIVE";

        using var kind = await PostAsync(tenantId, "periods", unknownKind);
        using var field = await PostAsync(tenantId, "periods", unknownField);

        await kind.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        await field.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found()
    {
        using var response = await PostAsync(new Guid("0192a1b2-0000-7000-8000-00000000c404"), "periods", Assign(Today, Today.AddDays(179)));

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task Renew_adds_a_period_from_the_day_after_the_latest_ends_on_the_plan_sent()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var assigned = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-100), Today.AddDays(20), plan: "PRO"));

        using var response = await PostAsync(tenantId, "renew", new Dictionary<string, object?>
        {
            ["endsOn"] = Date(Today.AddDays(385)),
            ["planCode"] = "STANDARD",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = await ReadAsync(response);
        subscription.Periods.Count.ShouldBe(2);
        var renewed = subscription.Periods[0];
        subscription.NextPeriodId.ShouldBe(renewed.Id);
        (renewed.PlanCode, renewed.Status, renewed.StartsOn, renewed.EndsOn)
            .ShouldBe(("STANDARD", SubscriptionStatus.Scheduled, Today.AddDays(21), Today.AddDays(385)));
        subscription.Periods[1].Status.ShouldBe(SubscriptionStatus.Expiring);
        (await AuditEntriesAsync()).Single(e => e.ActionCode == "subscription.renew").EntityId.ShouldBe(renewed.Id);
    }

    [Fact]
    public async Task Renew_without_a_period_or_to_a_day_before_its_first_day_is_refused()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var none = await PostAsync(tenantId, "renew", new Dictionary<string, object?> { ["endsOn"] = Date(Today.AddDays(365)) });
        using var assigned = await PostAsync(tenantId, "periods", Assign(Today, Today.AddDays(179)));

        using var early = await PostAsync(tenantId, "renew", new Dictionary<string, object?> { ["endsOn"] = Date(Today.AddDays(179)) });

        await none.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_NOT_FOUND");
        var problem = await early.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem().GetProperty("field").GetString().ShouldBe("endsOn");
        (await RowsAsync(tenantId)).ShouldHaveSingleItem();
    }

    // S04-T2 acceptance: period history is never overwritten (change plan).
    [Fact]
    public async Task Change_plan_keeps_the_earlier_period_cancelled_and_adds_one_on_the_higher_plan_to_the_same_end()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var assigned = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-30), Today.AddDays(150)));
        var original = (await ReadAsync(assigned)).Periods.Single();

        using var response = await PostAsync(tenantId, "change-plan", new Dictionary<string, object?> { ["planCode"] = "PRO" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = await ReadAsync(response);
        var current = subscription.Periods.Single(p => p.Id == subscription.CurrentPeriodId);
        (current.PlanCode, current.StartsOn, current.EndsOn, current.Status)
            .ShouldBe(("PRO", Today, original.EndsOn, SubscriptionStatus.Active));
        (await RowsAsync(tenantId)).ShouldBe(
            [(original.Id, "STANDARD", true, "Changed to PRO"), (current.Id, "PRO", false, null)], ignoreOrder: true);
        (await AuditEntriesAsync()).Single(e => e.ActionCode == "subscription.change_plan").EntityId.ShouldBe(current.Id);
    }

    [Fact]
    public async Task Change_plan_to_a_lower_plan_waits_for_the_renewal_and_needs_a_period_in_effect()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var none = await PostAsync(tenantId, "change-plan", new Dictionary<string, object?> { ["planCode"] = "PRO" });
        using var assigned = await PostAsync(tenantId, "periods", Assign(Today, Today.AddDays(179), plan: "PRO"));

        using var lower = await PostAsync(tenantId, "change-plan", new Dictionary<string, object?> { ["planCode"] = "STANDARD" });
        using var unknown = await PostAsync(tenantId, "change-plan", new Dictionary<string, object?> { ["planCode"] = "GOLD" });

        await none.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_NOT_FOUND");
        await lower.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        (await RowsAsync(tenantId)).ShouldHaveSingleItem().Cancelled.ShouldBeFalse();
    }

    // S04-T2 acceptance: period history is never overwritten (cancel).
    [Fact]
    public async Task Cancel_cancels_the_current_and_scheduled_periods_with_the_reason_and_keeps_the_rows()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        using var current = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-5), Today.AddDays(40)));
        using var next = await PostAsync(tenantId, "periods", Assign(Today.AddDays(41), Today.AddDays(400)));

        using var response = await PostAsync(tenantId, "cancel", new Dictionary<string, object?> { ["reason"] = $"  {_reason} " });
        using var again = await PostAsync(tenantId, "cancel", new Dictionary<string, object?> { ["reason"] = _reason });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = await ReadAsync(response);
        subscription.Periods.ShouldAllBe(p => p.Status == SubscriptionStatus.Cancelled && p.CancelReason == _reason);
        subscription.Periods.ShouldAllBe(p => p.CancelledBy == TenantTestData.StaffId && p.CancelledAt == App.Clock.GetUtcNow());
        subscription.CurrentPeriodId.ShouldBe(subscription.Periods[0].Id);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SUBSCRIPTION_NOT_FOUND");
        (await RowsAsync(tenantId)).ShouldAllBe(row => row.Cancelled && row.CancelReason == _reason);
        (await RowsAsync(tenantId)).Count.ShouldBe(2);
        var entry = (await AuditEntriesAsync()).Single(e => e.ActionCode == "subscription.cancel" && e.ErrorCode == null);
        (entry.Reason, entry.TenantId, entry.EntityId).ShouldBe((_reason, tenantId, subscription.Periods[1].Id));
    }

    [Fact]
    public async Task Cancel_needs_a_reason()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        using var response = await PostAsync(tenantId, "cancel", new Dictionary<string, object?> { ["reason"] = " " });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
    }

    [Fact]
    public async Task Get_lists_the_periods_latest_first_with_their_status_today_and_is_audited()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var past = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-400), Today.AddDays(-200)));
        using var current = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-199), Today.AddDays(10)));

        using var response = await GetAsync(tenantId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subscription = await ReadAsync(response);
        subscription.Periods.Select(p => p.Status).ShouldBe([SubscriptionStatus.Expiring, SubscriptionStatus.Expired]);
        subscription.CurrentPeriodId.ShouldBe(subscription.Periods[0].Id);
        (await AuditEntriesAsync()).Single(e => e.ActionCode == "subscription.read").TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task A_tenant_without_periods_has_no_current_period()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);

        using var response = await GetAsync(tenantId);

        var subscription = await ReadAsync(response);
        subscription.Periods.ShouldBeEmpty();
        subscription.CurrentPeriodId.ShouldBeNull();
    }

    // S04-T2 acceptance: day boundaries in the tenant's time zone, not UTC.
    [Fact]
    public async Task A_period_ends_at_midnight_in_the_tenants_time_zone()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        using var assigned = await PostAsync(tenantId, "periods", Assign(Today.AddDays(-30), Today, windowDays: 0, graceDays: 0));
        var kampalaMidnight = new DateTimeOffset(Today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));

        App.Clock.Advance(kampalaMidnight - App.Clock.GetUtcNow() - TimeSpan.FromSeconds(1));
        using var lastSecond = await GetAsync(tenantId);
        App.Clock.Advance(TimeSpan.FromSeconds(1));
        using var nextDay = await GetAsync(tenantId);

        (await ReadAsync(lastSecond)).Periods.Single().Status.ShouldBe(SubscriptionStatus.Active);
        (await ReadAsync(nextDay)).Periods.Single().Status.ShouldBe(SubscriptionStatus.Expired);
        App.Clock.GetUtcNow().UtcDateTime.Hour.ShouldBe(21);
    }

    private static Dictionary<string, object?> Assign(
        DateOnly startsOn, DateOnly endsOn, string plan = "STANDARD", string kind = "PAID", int? windowDays = null, int? graceDays = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["planCode"] = plan,
            ["kind"] = kind,
            ["startsOn"] = Date(startsOn),
            ["endsOn"] = Date(endsOn),
        };
        if (windowDays is not null)
        {
            body["expiringWindowDays"] = windowDays;
        }

        if (graceDays is not null)
        {
            body["graceDays"] = graceDays;
        }

        return body;
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<SubscriptionResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(Json, Ct)).ShouldNotBeNull();

    private async Task<HttpResponseMessage> PostAsync(Guid tenantId, string path, Dictionary<string, object?> body)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);
        return await client.PostAsJsonAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/subscription/{path}", UriKind.Relative), body, Ct);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid tenantId)
    {
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);
        return await client.GetAsync(new Uri($"{TenantTestData.TenantsPath}/{tenantId}/subscription", UriKind.Relative), Ct);
    }

    /// <summary>The audit entries, after delivering the outbox: a command's entry is an outbox message until then.</summary>
    private async Task<List<AuditEntry>> AuditEntriesAsync()
    {
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }

    /// <summary>The tenant's rows as the platform role reads them (every tenant, no RLS): id, plan, cancelled, reason.</summary>
    private Task<List<(Guid Id, string PlanCode, bool Cancelled, string? CancelReason)>> RowsAsync(Guid tenantId) => QueryAsync(
        DatabaseRoles.Platform,
        "SELECT id, plan_code, cancelled_at IS NOT NULL, cancel_reason FROM tenancy.subscription_periods WHERE tenant_id = @tenant",
        reader => (reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3)),
        new NpgsqlParameter("tenant", tenantId));
}
