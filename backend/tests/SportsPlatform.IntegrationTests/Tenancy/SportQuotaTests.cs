using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Tenancy.TenantSportRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// <c>MAX_ENABLED_SPORTS</c> on <c>tenant.sport.enable</c> (S05 D13, D18), with the test-only variants of
/// <see cref="TestSportDefinitions"/>: distinct sports count, so a second variant of a counted sport is never refused;
/// another sport at the limit is <c>403 ENTITLEMENT_QUOTA_EXCEEDED</c> under the tenant's sport lock; lowering the limit
/// disables nothing. KMSA's limit comes from an override (STANDARD allows 3).
/// </summary>
public sealed class SportQuotaTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // T5 acceptance: a STANDARD tenant enables up to its limit; two variants of one sport count once; the next sport is
    // refused with "Your plan allows N sports."
    [Fact]
    public async Task Two_variants_of_one_sport_count_once_and_the_next_sport_is_refused_at_the_limit()
    {
        await using var app = AppWithTestSports(App);
        using var owner = await KmsaOwnerAsync(app, limit: 1);

        (await owner.EnableSportAsync(TestballFull, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var secondVariant = await owner.EnableSportAsync(TestballShort, Ct);
        secondVariant.StatusCode.ShouldBe(HttpStatusCode.OK);
        Quota(await secondVariant.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .ShouldBe(new QuotaUsageResponse("MAX_ENABLED_SPORTS", Measured: true, Used: 1, Limit: 1, Warning: true));

        using var refused = await owner.EnableSportAsync(Testhoop, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 1 sports.");
        var denied = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "tenant.sport.enable" && e.Outcome != AuditOutcome.Success);
        (denied.ErrorCode, denied.EntityType, denied.EntityId).ShouldBe(("ENTITLEMENT_QUOTA_EXCEEDED", "TenantSport", (Guid?)null));
        denied.Metadata.ShouldNotBeNull().ShouldContain(Testhoop);
    }

    [Fact]
    public async Task A_standard_tenant_enables_three_sports()
    {
        await using var app = AppWithTestSports(App);
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var owner = ClientFor(app, grace.UserId, kmsa);

        foreach (var variant in new[] { Football, TestballFull, TestballShort, Testhoop })
        {
            (await owner.EnableSportAsync(variant, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var read = await owner.GetAsync(TenantSportsPath, Ct);
        Quota(await read.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .ShouldBe(new QuotaUsageResponse("MAX_ENABLED_SPORTS", Measured: true, Used: 3, Limit: 3, Warning: true));
    }

    // T5 acceptance: two enables at the last free sport at the same time queue on the tenant's sport lock. The test holds
    // the lock first and releases it only once both requests wait on it, so they really are at the same point; without
    // the lock in the handler they would never wait and the test would time out (#88 R3).
    [Fact]
    public async Task Two_enables_at_the_last_free_sport_at_once_let_one_through()
    {
        await using var app = AppWithTestSports(App);
        using var owner = await KmsaOwnerAsync(app, limit: 1);
        var kmsa = Guid.Parse(owner.DefaultRequestHeaders.GetValues("X-Tenant-Id").Single());

        await using var holder = new NpgsqlConnection(Fixture.ConnectionString(DatabaseRoles.App));
        await holder.OpenAsync(Ct);
        await using var held = await holder.BeginTransactionAsync(Ct);
        await using (var take = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", holder, held))
        {
            take.Parameters.AddWithValue("key", $"tenancy.tenant_sports:{kmsa}");
            await take.ExecuteNonQueryAsync(Ct);
        }

        var enables = Task.WhenAll(owner.EnableSportAsync(TestballFull, Ct), owner.EnableSportAsync(Testhoop, Ct));
        await WaitForLockWaitersAsync(holder, count: 2);
        await held.CommitAsync(Ct);
        var responses = await enables;
        using var first = responses[0];
        using var second = responses[1];

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        await responses.Single(r => r.StatusCode != HttpStatusCode.OK).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
    }

    // D13: lowering the limit never disables a sport; disabling one frees its place.
    [Fact]
    public async Task Over_a_lowered_limit_the_sports_stay_and_disabling_one_frees_a_place_only_below_the_limit()
    {
        await using var app = AppWithTestSports(App);
        using var owner = await KmsaOwnerAsync(app, limit: 0);
        var kmsa = Guid.Parse(owner.DefaultRequestHeaders.GetValues("X-Tenant-Id").Single());
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);

        (await owner.EnableSportAsync(TestballShort, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.DisableSportAsync(Testhoop, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await owner.EnableSportAsync(Testhoop, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
    }

    /// <summary>
    /// Until <paramref name="count"/> sessions wait for an advisory lock (the requests queued behind the test's), checked
    /// every 50 ms for at most 30 s: a condition, not a fixed sleep.
    /// </summary>
    private static async Task WaitForLockWaitersAsync(NpgsqlConnection connection, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", connection);
            if ((long)(await command.ExecuteScalarAsync(Ct))! >= count)
            {
                return;
            }

            DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, $"{count} requests never waited on the tenant's sport lock.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    /// <summary>The owner's client of KMSA (test data's STANDARD period) with an override setting the sport limit.</summary>
    private async Task<HttpClient> KmsaOwnerAsync(WebApplicationFactory<Program> app, int limit)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await ExecuteAsync(
            DatabaseRoles.App,
            $"SELECT set_config('app.tenant_id', '{kmsa}', false); " +
            "INSERT INTO tenancy.tenant_entitlement_overrides (id, tenant_id, entitlement_code, value, reason, " +
            "created_at, created_by, updated_at, updated_by) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{kmsa}', 'MAX_ENABLED_SPORTS', '{{\"limit\": {limit}}}', 'Pilot agreement', " +
            $"now(), '{TenantTestData.StaffId}', now(), '{TenantTestData.StaffId}')");
        app.Services.GetRequiredService<TenantAccessCache>().Clear(kmsa);
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        return ClientFor(app, grace.UserId, kmsa);
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> app, Guid userId, Guid tenantId)
    {
        var client = app.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static QuotaUsageResponse Quota(JsonElement body) =>
        body.GetProperty("sportQuota").Deserialize<QuotaUsageResponse>(Json).ShouldNotBeNull();
}
