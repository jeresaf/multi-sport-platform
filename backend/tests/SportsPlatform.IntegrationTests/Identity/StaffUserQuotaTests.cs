using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>MAX_STAFF_USERS</c> on <c>membership.invite</c> (S04 D12, D13; pack spec 07 §30 steps 11–12): members who aren't
/// removed and pending, unexpired invitations take seats; an invitation past the limit is
/// <c>403 ENTITLEMENT_QUOTA_EXCEEDED</c> under the tenant's staff-user lock; the usage shows on the tenant's and the
/// platform's subscription reads, with the warning from 80 %. KMSA's limit comes from an override (STANDARD allows 100).
/// </summary>
public sealed class StaffUserQuotaTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public const string TenantSubscriptionPath = "/api/admin/v1/tenant/subscription";
    private const string _reason = "Pilot agreement: five staff users";

    // §30 11–12: the usage warns at 4 of 5; the fifth seat is taken; the next invitation is refused, a re-invitation
    // that replaces a pending one is not.
    [Fact]
    public async Task Invitations_take_seats_with_a_warning_from_80_percent_then_stop_at_the_limit()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync(limit: 5);
        await CreateMemberAsync(App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var owner = App.ClientFor(grace.UserId, kmsa);
        (await StaffUsageAsync(owner)).ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", Measured: true, Used: 3, Limit: 5, Warning: false));

        await owner.InvitedAsync("brian.ssemakula@kmsa.example", Ct, IdentityRoles.Registrar);
        (await StaffUsageAsync(owner)).ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", Measured: true, Used: 4, Limit: 5, Warning: true));

        await owner.InvitedAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Viewer);
        using var refused = await owner.InviteAsync("sarah.namuli@kmsa.example", Ct, IdentityRoles.Viewer);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 5 staff users.");
        (await StaffUsageAsync(owner)).ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", Measured: true, Used: 5, Limit: 5, Warning: true));
        (await owner.InviteAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Registrar)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_removed_member_or_an_expired_invitation_frees_a_seat_and_a_suspended_member_keeps_one()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync(limit: 3);
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var owner = App.ClientFor(grace.UserId, kmsa);
        await owner.InvitedAsync("brian.ssemakula@kmsa.example", Ct, IdentityRoles.Registrar);
        await (await owner.InviteAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Viewer))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");

        (await owner.SuspendAsync(daniel.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await owner.InviteAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Viewer))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");

        (await owner.RemoveAsync(daniel.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.InviteAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Viewer)).StatusCode.ShouldBe(HttpStatusCode.Created);
        await (await owner.InviteAsync("sarah.namuli@kmsa.example", Ct, IdentityRoles.Viewer))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");

        // Invitations expire 7 days after they were made.
        App.Clock.Advance(TimeSpan.FromDays(7));
        (await StaffUsageAsync(owner)).Used.ShouldBe(1);
        (await owner.InviteAsync("sarah.namuli@kmsa.example", Ct, IdentityRoles.Viewer)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // T3 acceptance: two invitations at the last free seat at the same time queue on the tenant's staff-user lock.
    [Fact]
    public async Task Two_invitations_at_the_last_free_seat_at_once_let_one_through()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync(limit: 2);
        using var owner = App.ClientFor(grace.UserId, kmsa);

        var responses = await Task.WhenAll(
            owner.InviteAsync("brian.ssemakula@kmsa.example", Ct, IdentityRoles.Registrar),
            owner.InviteAsync("agnes.akello@kmsa.example", Ct, IdentityRoles.Viewer));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        await responses.Single(r => r.StatusCode != HttpStatusCode.Created)
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        (await StaffUsageAsync(owner)).Used.ShouldBe(2);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    // S04 D13: staff can always give a tenant an owner; the invitation still takes a seat.
    [Fact]
    public async Task The_platform_invites_an_owner_past_the_limit_and_the_seat_counts()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync(limit: 1);
        var adminId = await AccountTestData.CreateUserAsync(App.Services, "asha.mutesi@msp.example", TestPasswords.New(), Ct, platformRole: PlatformRoles.Admin);
        using var platform = App.CreateClient().AsUser(adminId);

        using var invited = await platform.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { email = "isaac.mugisha@kmsa.example" }, Ct);

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var owner = App.ClientFor(grace.UserId, kmsa);
        (await StaffUsageAsync(owner)).ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", Measured: true, Used: 2, Limit: 1, Warning: true));
        using var read = await platform.GetAsync($"{TenantTestData.TenantsPath}/{kmsa}/subscription", Ct);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var usage = (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("usage");
        UsageOf(usage, "MAX_STAFF_USERS").ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", Measured: true, Used: 2, Limit: 1, Warning: true));
        UsageOf(usage, "MAX_TEAMS").ShouldBe(new QuotaUsageResponse("MAX_TEAMS", Measured: false, Used: null, Limit: 120, Warning: false));
    }

    // S04 D15: every member who runs the tenant or reads it as a viewer sees the plan, its values and usage, never the
    // platform's override reason; other roles don't.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, HttpStatusCode.OK)]
    [InlineData(IdentityRoles.TenantAdmin, HttpStatusCode.OK)]
    [InlineData(IdentityRoles.Viewer, HttpStatusCode.OK)]
    [InlineData(IdentityRoles.Registrar, HttpStatusCode.Forbidden)]
    public async Task Members_read_the_tenants_subscription_without_the_platforms_reasons(string role, HttpStatusCode expected)
    {
        var (kmsa, _) = await KmsaWithOwnerAsync(limit: 5);
        var member = await CreateMemberAsync(App.Services, kmsa, MailboxOf(role, "kmsa.example"), Ct, TenantWide(role));
        using var client = App.ClientFor(member.UserId, kmsa);

        using var response = await client.GetAsync(TenantSubscriptionPath, Ct);

        response.StatusCode.ShouldBe(expected);
        if (expected != HttpStatusCode.OK)
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            return;
        }

        var text = await response.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain(_reason);
        var body = JsonDocument.Parse(text).RootElement;
        body.GetProperty("timeZone").GetString().ShouldBe("Africa/Kampala");
        var current = body.GetProperty("current");
        (current.GetProperty("planCode").GetString(), current.GetProperty("planName").GetString(), current.GetProperty("status").GetString())
            .ShouldBe(("STANDARD", "Standard", "ACTIVE"));
        (current.GetProperty("startsOn").GetString(), current.GetProperty("endsOn").GetString())
            .ShouldBe(("2026-01-01", "2027-12-31"));
        body.GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);

        var staff = body.GetProperty("entitlements").EnumerateArray().Single(e => e.GetProperty("code").GetString() == "MAX_STAFF_USERS");
        (staff.GetProperty("name").GetString(), staff.GetProperty("type").GetString(), staff.GetProperty("unit").GetString())
            .ShouldBe(("Staff users", "QUOTA", "users"));
        (staff.GetProperty("value").GetProperty("limit").GetInt32(), staff.GetProperty("source").GetString()).ShouldBe((5, "OVERRIDE"));
        UsageOf(body.GetProperty("usage"), "MAX_STAFF_USERS").ShouldBe(new QuotaUsageResponse("MAX_STAFF_USERS", true, 2, 5, Warning: false));
        UsageOf(body.GetProperty("usage"), "STORAGE_GB").Measured.ShouldBeFalse();
    }

    // Reads go on while read-only (S04 D8): the page says why, and an invitation is refused by step 1 first.
    [Fact]
    public async Task A_read_only_tenant_reads_its_subscription_and_cant_invite()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var owner = App.ClientFor(grace.UserId, kmsa);

        using var response = await owner.GetAsync(TenantSubscriptionPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("Pilot paused");
        JsonDocument.Parse(text).RootElement.GetProperty("current").GetProperty("status").GetString().ShouldBe("CANCELLED");
        await (await owner.InviteAsync("brian.ssemakula@kmsa.example", Ct, IdentityRoles.Registrar))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    /// <summary>KMSA (test data's STANDARD period) with its owner and an override setting <c>MAX_STAFF_USERS</c> to <paramref name="limit"/>.</summary>
    private async Task<(Guid Tenant, Member Owner)> KmsaWithOwnerAsync(int limit)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await ExecuteAsync(
            DatabaseRoles.App,
            $"SELECT set_config('app.tenant_id', '{kmsa}', false); " +
            "INSERT INTO tenancy.tenant_entitlement_overrides (id, tenant_id, entitlement_code, value, reason, " +
            "created_at, created_by, updated_at, updated_by) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{kmsa}', 'MAX_STAFF_USERS', '{{\"limit\": {limit}}}', '{_reason}', " +
            $"now(), '{TenantTestData.StaffId}', now(), '{TenantTestData.StaffId}')");
        App.Services.GetRequiredService<TenantAccessCache>().Clear(kmsa);
        return (kmsa, await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner)));
    }

    private static async Task<QuotaUsageResponse> StaffUsageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(TenantSubscriptionPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return UsageOf((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("usage"), "MAX_STAFF_USERS");
    }

    private static QuotaUsageResponse UsageOf(JsonElement usage, string code) =>
        usage.EnumerateArray().Single(u => u.GetProperty("code").GetString() == code).Deserialize<QuotaUsageResponse>(Json).ShouldNotBeNull();
}
