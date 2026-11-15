using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Tenancy.TenantSportRequests;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Tenant sport enablement with the seeded football definition (S05 D12, D14, D15, D19): the tenant's sports read,
/// enable and disable with their errors and audit, the history kept in <c>tenancy.tenant_sports</c>, the roles, the
/// subscription's usage and <see cref="IEnabledSports"/>. The quota's counting is in <see cref="SportQuotaTests"/>.
/// </summary>
public sealed class TenantSportTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_tenants_sports_list_every_published_variant_and_whether_it_is_enabled_with_the_quota()
    {
        var (kmsa, owner) = await KmsaAsync(IdentityRoles.TenantOwner);

        var before = await ReadAsync(owner);
        var football = before.GetProperty("variants").EnumerateArray().Single();
        (football.GetProperty("variant").GetString(), football.GetProperty("sport").GetString(), football.GetProperty("name").GetString(),
            football.GetProperty("scoringFamily").GetString(), football.GetProperty("enabled").GetBoolean())
            .ShouldBe((Football, "FOOTBALL", "Football", "ADDITIVE", false));
        football.GetProperty("enabledSince").ValueKind.ShouldBe(JsonValueKind.Null);
        Quota(before).ShouldBe(new QuotaUsageResponse("MAX_ENABLED_SPORTS", Measured: true, Used: 0, Limit: 3, Warning: false));

        using var enabled = await owner.EnableSportAsync(Football, Ct);

        enabled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await enabled.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var row = after.GetProperty("variants").EnumerateArray().Single();
        row.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        row.GetProperty("enabledSince").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow());
        Quota(after).ShouldBe(new QuotaUsageResponse("MAX_ENABLED_SPORTS", Measured: true, Used: 1, Limit: 3, Warning: false));
        JsonElement.DeepEquals(await ReadAsync(owner), after).ShouldBeTrue();
        var entry = (await AuditEntriesAsync()).Single(e => e.ActionCode == "tenant.sport.enable");
        (entry.Outcome, entry.TenantId, entry.EntityType, entry.EntityId).ShouldBe((AuditOutcome.Success, kmsa, "TenantSport", await OpenRowIdAsync(kmsa)));
        entry.Metadata.ShouldNotBeNull().ShouldContain("FOOTBALL");
    }

    // T5 acceptance: disabling and enabling again keeps both enablements in tenancy.tenant_sports.
    [Fact]
    public async Task Disabling_and_enabling_again_keeps_both_enablements_in_the_table()
    {
        var (kmsa, owner) = await KmsaAsync(IdentityRoles.TenantAdmin);
        (await owner.EnableSportAsync(Football, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        App.Clock.Advance(TimeSpan.FromDays(30));

        using var disabled = await owner.DisableSportAsync(Football, Ct, reason: "  Football moves to the next season  ");

        disabled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterDisable = await disabled.Content.ReadFromJsonAsync<JsonElement>(Ct);
        afterDisable.GetProperty("variants")[0].GetProperty("enabled").GetBoolean().ShouldBeFalse();
        Quota(afterDisable).Used.ShouldBe(0);
        App.Clock.Advance(TimeSpan.FromDays(30));
        (await owner.EnableSportAsync(Football, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var rows = await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT variant, enabled_at, disabled_at, disable_reason FROM tenancy.tenant_sports WHERE tenant_id = @tenant ORDER BY enabled_at",
            reader => (reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.IsDBNull(2) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)),
            new NpgsqlParameter("tenant", kmsa));
        var start = App.Clock.GetUtcNow().AddDays(-60);
        rows.ShouldBe([
            (Football, start, start.AddDays(30), "Football moves to the next season"),
            (Football, start.AddDays(60), null, null),
        ]);
        var disable = (await AuditEntriesAsync()).Single(e => e.ActionCode == "tenant.sport.disable");
        (disable.Outcome, disable.Reason, disable.EntityType).ShouldBe((AuditOutcome.Success, "Football moves to the next season", "TenantSport"));
    }

    [Fact]
    public async Task Enabling_an_enabled_variant_or_disabling_one_that_isnt_is_a_conflict()
    {
        var (_, owner) = await KmsaAsync(IdentityRoles.TenantOwner);
        await (await owner.DisableSportAsync(Football, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SPORT_NOT_ENABLED");
        (await owner.EnableSportAsync(Football, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await owner.EnableSportAsync(Football, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "TENANT_SPORT_ALREADY_ENABLED");
        var refused = (await AuditEntriesAsync()).Where(e => e.Outcome != AuditOutcome.Success).ToList();
        refused.Select(e => (e.ActionCode, e.ErrorCode, e.EntityType, e.EntityId, e.Reason)).ShouldBe(
        [
            ("tenant.sport.disable", "TENANT_SPORT_NOT_ENABLED", "TenantSport", (Guid?)null, "End of the schools season"),
            ("tenant.sport.enable", "TENANT_SPORT_ALREADY_ENABLED", "TenantSport", null, null),
        ], ignoreOrder: true);
        refused.ShouldAllBe(e => e.Metadata!.Contains(Football));
    }

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public async Task An_unknown_or_differently_cased_variant_is_refused_on_the_field(string command)
    {
        var (_, owner) = await KmsaAsync(IdentityRoles.TenantOwner);

        foreach (var variant in new[] { "CRICKET_T20_KAMPALA", "football_association" })
        {
            using var response = command == "enable" ? await owner.EnableSportAsync(variant, Ct) : await owner.DisableSportAsync(variant, Ct);

            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "TENANT_SPORT_VARIANT_UNKNOWN");
            problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldBe(["variant"]);
        }
    }

    // #88 R5: the variant comes from the path, so the audit keeps no more of an unknown one than a variant code's length.
    [Fact]
    public async Task A_long_unknown_variant_is_audited_cut_to_a_variant_codes_length()
    {
        var (_, owner) = await KmsaAsync(IdentityRoles.TenantOwner);
        var variant = new string('X', 60);

        await (await owner.EnableSportAsync(variant, Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "TENANT_SPORT_VARIANT_UNKNOWN");

        var refused = (await AuditEntriesAsync()).Single(e => e.ActionCode == "tenant.sport.enable");
        refused.Metadata.ShouldNotBeNull().ShouldContain($"\"{new string('X', 50)}\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Disabling_needs_a_reason(string? reason)
    {
        var (_, owner) = await KmsaAsync(IdentityRoles.TenantOwner);
        (await owner.EnableSportAsync(Football, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await owner.DisableSportAsync(Football, Ct, reason)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        (await ReadAsync(owner)).GetProperty("variants")[0].GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_disable_reason_longer_than_500_characters_is_refused()
    {
        var (_, owner) = await KmsaAsync(IdentityRoles.TenantOwner);
        (await owner.EnableSportAsync(Football, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await owner.DisableSportAsync(Football, Ct, new string('r', 501))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        (await owner.DisableSportAsync(Football, Ct, new string('r', 500))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // D14, D15: everyone who reads the sport's words reads the tenant's sports; the quota only with the subscription
    // read; only the owner and administrators enable and disable.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Viewer, true, false)]
    [InlineData(IdentityRoles.Registrar, false, false)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Members_read_the_tenants_sports_and_only_administrators_change_them(string role, bool seesQuota, bool changes)
    {
        var (kmsa, client) = await KmsaAsync(role);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Football, Ct);

        var read = await ReadAsync(client);
        (read.GetProperty("sportQuota").ValueKind != JsonValueKind.Null).ShouldBe(seesQuota);

        using var disabled = await client.DisableSportAsync(Football, Ct);
        using var enabled = await client.EnableSportAsync(Football, Ct);
        if (changes)
        {
            (disabled.StatusCode, enabled.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK));
        }
        else
        {
            await disabled.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            await enabled.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    // D13: Settings → Subscription and the console show the sport quota's usage as they do the staff users'.
    [Fact]
    public async Task The_subscription_reads_measure_enabled_sports()
    {
        var (kmsa, owner) = await KmsaAsync(IdentityRoles.TenantOwner);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Football, Ct);

        using var response = await owner.GetAsync("/api/admin/v1/tenant/subscription", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var usage = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("usage").EnumerateArray()
            .Single(u => u.GetProperty("code").GetString() == "MAX_ENABLED_SPORTS").Deserialize<QuotaUsageResponse>(Json);
        usage.ShouldBe(new QuotaUsageResponse("MAX_ENABLED_SPORTS", Measured: true, Used: 1, Limit: 3, Warning: false));
    }

    [Fact]
    public async Task IEnabledSports_answers_for_the_scopes_tenant_only()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Football, Ct);

        (await EnabledSportsOfAsync(kmsa)).ShouldBe([new EnabledSport(Football, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero))]);
        (await EnabledSportsOfAsync(elssc)).ShouldBeEmpty();

        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(elssc);
        (await scope.ServiceProvider.GetRequiredService<IEnabledSports>().IsEnabledAsync(Football, Ct)).ShouldBeFalse();
    }

    // Successful entries arrive through the outbox (events-and-jobs.md, "Audit as built").
    private async Task<List<AuditEntry>> AuditEntriesAsync()
    {
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }

    private async Task<IReadOnlyList<EnabledSport>> EnabledSportsOfAsync(Guid tenantId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IEnabledSports>().ListAsync(Ct);
    }

    /// <summary>KMSA (test data's STANDARD period: 3 sports) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> KmsaAsync(string role)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, MailboxOf(role, "kmsa.example"), Ct, TenantWide(role));
        return (kmsa, App.ClientFor(member.UserId, kmsa));
    }

    private async Task<Guid> OpenRowIdAsync(Guid tenantId) =>
        (await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT id FROM tenancy.tenant_sports WHERE tenant_id = @tenant AND disabled_at IS NULL",
            reader => reader.GetGuid(0),
            new NpgsqlParameter("tenant", tenantId))).Single();

    private static async Task<JsonElement> ReadAsync(HttpClient client)
    {
        using var response = await client.GetAsync(TenantSportsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static QuotaUsageResponse Quota(JsonElement body) =>
        body.GetProperty("sportQuota").Deserialize<QuotaUsageResponse>(Json).ShouldNotBeNull();
}
