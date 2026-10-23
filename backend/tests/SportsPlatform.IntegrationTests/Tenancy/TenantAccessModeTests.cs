using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// Step 1 with roles (S03 D11), on the real membership check and evaluator: a <c>PROVISIONING</c> tenant is open to its
/// owners and admins only (<c>403 TENANT_PROVISIONING</c> for other members); a <c>BLOCKED</c> one refuses every tenant
/// action but <c>tenant.access.read</c>, which only its owner holds. With subscriptions (S04 D8): an <c>ACTIVE</c> one
/// whose current period has expired, was cancelled or hasn't started, or that has none, is <c>READ_ONLY</c>: every
/// tenant read passes, every other action is <c>403 TENANT_READ_ONLY</c>.
/// </summary>
public sealed class TenantAccessModeTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _accessPath = "/api/admin/v1/tenant/access";
    private const string _settingsPath = "/api/admin/v1/tenant/settings";
    private const string _timeZone = "Africa/Kampala";

    /// <summary>The period test data gives a tenant that isn't provisioning, as access sees it on the test clock.</summary>
    private static readonly TenantAccessPeriod _testPeriod = new(
        SubscriptionStatus.Active, TenantTestData.PeriodStartsOn, TenantTestData.PeriodEndsOn, TenantTestData.PeriodEndsOn.AddDays(14));

    private DateOnly Today => TenantCalendar.Today(App.Clock, _timeZone);

    [Theory]
    [InlineData(IdentityRoles.TenantOwner)]
    [InlineData(IdentityRoles.TenantAdmin)]
    public async Task Owners_and_admins_set_up_a_provisioning_tenant(string role)
    {
        var kmsa = await TenantAsync(TenantStatus.Provisioning);
        var member = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(role));
        using var client = App.ClientFor(member.UserId, kmsa);

        (await client.GetAsync(_settingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PatchSettingsAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(MembersPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Other_members_wait_until_a_provisioning_tenant_is_activated()
    {
        var kmsa = await TenantAsync(TenantStatus.Provisioning);
        var viewer = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var client = App.ClientFor(viewer.UserId, kmsa);

        // Actions the viewer holds, and one it doesn't: step 1 answers before the evaluator.
        await (await client.GetAsync(_settingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_PROVISIONING");
        await (await client.GetAsync(MembersPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_PROVISIONING");
        await (await PatchSettingsAsync(client)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_PROVISIONING");
    }

    [Fact]
    public async Task An_admin_whose_admin_role_is_revoked_waits_too()
    {
        var kmsa = await TenantAsync(TenantStatus.Provisioning);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var daniel = await CreateMemberAsync(
            App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin), TenantWide(IdentityRoles.Viewer));
        using var ownerClient = App.ClientFor(owner.UserId, kmsa);
        using var danielClient = App.ClientFor(daniel.UserId, kmsa);
        (await danielClient.GetAsync(_settingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var adminRole = (await (await ownerClient.GetAsync(MemberPath(daniel.MembershipId), Ct)).ReadMemberAsync(Ct))
            .Roles.Single(r => r.RoleCode == IdentityRoles.TenantAdmin).AssignmentId;
        (await ownerClient.RevokeAsync(daniel.MembershipId, adminRole, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await danielClient.GetAsync(_settingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_PROVISIONING");
    }

    [Theory]
    [InlineData(TenantStatus.Suspended, "Season fees outstanding")]
    [InlineData(TenantStatus.Archived, "Council merged into another association")]
    public async Task The_owner_of_a_blocked_tenant_sees_why_and_nothing_else(TenantStatus status, string reason)
    {
        var kmsa = await TenantAsync(status);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        using var response = await client.GetAsync(_accessPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var access = await ReadAccessAsync(response);
        access.ShouldBe(new TenantAccessResponse(status, TenantAccessMode.Blocked, reason, access.Since, _timeZone, _testPeriod));
        access.Since.ShouldBe(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"accessMode\":\"BLOCKED\"");

        await (await client.GetAsync(_settingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        await (await client.GetAsync(MembersPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        await (await PatchSettingsAsync(client)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
    }

    [Fact]
    public async Task A_second_suspension_shows_its_own_reason_and_time()
    {
        var kmsa = await TenantAsync(TenantStatus.Suspended);
        var restored = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
        var suspendedAgain = new DateTimeOffset(2026, 10, 12, 14, 30, 0, TimeSpan.Zero);
        await TenantTestData.ChangeAsync(App.Services, kmsa, t => t.Restore(SystemActor.Instance, restored).IsSuccess.ShouldBeTrue(), Ct);
        await TenantTestData.ChangeAsync(
            App.Services, kmsa, t => t.Suspend("Contract renewal unsigned", SystemActor.Instance, suspendedAgain).IsSuccess.ShouldBeTrue(), Ct);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        using var response = await client.GetAsync(_accessPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAccessAsync(response)).ShouldBe(
            new TenantAccessResponse(TenantStatus.Suspended, TenantAccessMode.Blocked, "Contract renewal unsigned", suspendedAgain, _timeZone, _testPeriod));
    }

    [Theory]
    [InlineData(IdentityRoles.TenantAdmin)]
    [InlineData(IdentityRoles.Viewer)]
    public async Task Other_members_of_a_blocked_tenant_are_refused_everything(string role)
    {
        var kmsa = await TenantAsync(TenantStatus.Suspended);
        var member = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(role));
        using var client = App.ClientFor(member.UserId, kmsa);

        // The access read passes step 1 but isn't theirs to hold (owner, plan 2026-10-20).
        await (await client.GetAsync(_accessPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await (await client.GetAsync(_settingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
        await (await client.GetAsync(MembersPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_BLOCKED");
    }

    [Theory]
    [InlineData(TenantStatus.Provisioning, TenantAccessMode.Full)]
    [InlineData(TenantStatus.Active, TenantAccessMode.Full)]
    public async Task The_owner_reads_the_access_of_a_working_tenant(TenantStatus status, TenantAccessMode mode)
    {
        var kmsa = await TenantAsync(status);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var admin = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        using var ownerClient = App.ClientFor(owner.UserId, kmsa);
        using var adminClient = App.ClientFor(admin.UserId, kmsa);

        using var response = await ownerClient.GetAsync(_accessPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var access = await ReadAccessAsync(response);
        // Test data gives an active tenant a period, a provisioning one none (S04 D9: setup needs none).
        access.ShouldBe(new TenantAccessResponse(
            status, mode, Reason: null, access.Since, _timeZone, status == TenantStatus.Active ? _testPeriod : null));
        await (await adminClient.GetAsync(_accessPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task The_access_read_is_audited_and_a_refused_one_too()
    {
        var kmsa = await TenantAsync(TenantStatus.Suspended);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var viewer = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var ownerClient = App.ClientFor(owner.UserId, kmsa);
        using var viewerClient = App.ClientFor(viewer.UserId, kmsa);

        (await ownerClient.GetAsync(_accessPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewerClient.GetAsync(_accessPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "tenant.access.read").ToList();
        entries.Count.ShouldBe(2);
        entries.ShouldContain(e => e.Outcome == AuditOutcome.Success && e.ActorId == owner.UserId && e.TenantId == kmsa
            && e.EntityType == "Tenant" && e.EntityId == kmsa);
        entries.ShouldContain(e => e.Outcome == AuditOutcome.Denied && e.ActorId == viewer.UserId && e.ErrorCode == "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task An_owner_of_another_tenant_reads_nothing_here()
    {
        var kmsa = await TenantAsync(TenantStatus.Suspended);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var owner = await CreateMemberAsync(App.Services, elssc, "ruth.achieng@elssc.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        using var response = await client.GetAsync(_accessPath, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("Season fees");
    }

    public static TheoryData<string> ReadOnlyCases => ["none", "expired", "cancelled", "scheduled"];

    [Theory]
    [MemberData(nameof(ReadOnlyCases))]
    public async Task A_read_only_tenant_reads_its_data_and_is_refused_a_write(string subscription)
    {
        var (kmsa, expected) = await ReadOnlyTenantAsync(subscription);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        // Every tenant read passes step 1: the settings, the members, the capabilities and the access.
        (await client.GetAsync(_settingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(MembersPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/admin/v1/me/capabilities", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var response = await client.GetAsync(_accessPath, Ct);
        var access = await ReadAccessAsync(response);
        access.ShouldBe(new TenantAccessResponse(TenantStatus.Active, TenantAccessMode.ReadOnly, Reason: null, access.Since, _timeZone, expected));
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"accessMode\":\"READ_ONLY\"");

        // Writes are refused before the evaluator, whoever holds them.
        await (await PatchSettingsAsync(client)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        await (await client.InviteAsync("daniel.kato@kmsa.example", Ct, IdentityRoles.Viewer))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "tenant.settings.update").ToList();
        entries.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.ErrorCode.ShouldBe("TENANT_READ_ONLY"),
            e => e.TenantId.ShouldBe(kmsa));
    }

    // Every mapped admin endpoint, read from the app's own endpoints like the authorization matrix: under READ_ONLY,
    // step 1 refuses each action not available when read-only and lets every other through (#66 R2).
    [Fact]
    public async Task Under_read_only_every_admin_endpoint_is_refused_unless_its_action_is_available_when_read_only()
    {
        // No period: READ_ONLY. Every action allowed past step 3, so only step 1 decides.
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        App.Authorization.AllowAll();
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId, kmsa);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
        var endpoints = AdminEndpoints();
        endpoints.ShouldContain(e => e.Action.AvailableWhenReadOnly);
        endpoints.ShouldContain(e => !e.Action.AvailableWhenReadOnly);

        var failures = new List<string>();
        foreach (var (method, path, action) in endpoints)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request, Ct);
            var code = await CodeOfAsync(response);
            if (action.AvailableWhenReadOnly ? code == "TENANT_READ_ONLY" : (response.StatusCode, code) != (HttpStatusCode.Forbidden, "TENANT_READ_ONLY"))
            {
                failures.Add($"{method} {path} ({action.Code}, available when read-only: {action.AvailableWhenReadOnly}) answered {(int)response.StatusCode} {code}.");
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>The admin endpoints that require a tenant action, one per method, with GUID route parameters filled.</summary>
    private List<(string Method, string Path, ActionDefinition Action)> AdminEndpoints() =>
    [
        .. App.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetOrderedMetadata<RequiresActionMetadata>() is [{ Action.Scope: ActionScope.Tenant }])
            .Select(e => (Endpoint: e, Path: "/" + string.Join('/', e.RoutePattern.PathSegments.Select(segment => string.Concat(
                segment.Parts.Select(part => part switch
                {
                    RoutePatternLiteralPart literal => literal.Content,
                    RoutePatternSeparatorPart separator => separator.Content,
                    RoutePatternParameterPart parameter => Guid.CreateVersion7().ToString(),
                    _ => throw new InvalidOperationException($"Unexpected route part {part.GetType().Name}."),
                }))))))
            .Where(e => e.Path.StartsWith("/api/admin/", StringComparison.Ordinal))
            .SelectMany(e => (e.Endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (method, e.Path, e.Endpoint.Metadata.GetRequiredMetadata<RequiresActionMetadata>().Action))),
    ];

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return null;
        }

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Renewing_restores_writes_on_the_next_request()
    {
        var (kmsa, _) = await ReadOnlyTenantAsync("expired");
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);
        await (await PatchSettingsAsync(client)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");

        var adminId = await AccountTestData.CreateUserAsync(App.Services, "asha.mutesi@msp.example", TestPasswords.New(), Ct, platformRole: PlatformRoles.Admin);
        using var admin = App.CreateClient().AsUser(adminId);
        using var renewed = await admin.PostAsJsonAsync(
            new Uri($"{TenantTestData.TenantsPath}/{kmsa}/subscription/renew", UriKind.Relative), new { endsOn = Today.AddDays(180) }, Ct);
        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PatchSettingsAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAccessAsync(await client.GetAsync(_accessPath, Ct))).AccessMode.ShouldBe(TenantAccessMode.Full);
    }

    [Fact]
    public async Task Access_turns_read_only_at_midnight_in_the_tenants_time_zone_after_the_last_day_of_grace()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        var today = Today;
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, today.AddDays(-100), today.AddDays(-14), Ct); // grace ends today
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        // Kampala is UTC+3 all year: its midnight is 21:00 UTC the day before.
        var midnight = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        App.Clock.Advance(midnight - App.Clock.GetUtcNow() - TimeSpan.FromSeconds(1));
        (await ReadAccessAsync(await client.GetAsync(_accessPath, Ct))).Subscription.ShouldNotBeNull().Status.ShouldBe(SubscriptionStatus.Grace);
        (await PatchSettingsAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        (await ReadAccessAsync(await client.GetAsync(_accessPath, Ct))).Subscription.ShouldNotBeNull().Status.ShouldBe(SubscriptionStatus.Expired);
        await (await PatchSettingsAsync(client)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    /// <summary>
    /// An active tenant made read-only by its subscription: none; one that ended past its grace; the test period
    /// cancelled; or one that starts tomorrow. Returns the current period as access sees it (null for none).
    /// </summary>
    private async Task<(Guid TenantId, TenantAccessPeriod? Current)> ReadOnlyTenantAsync(string subscription)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        var today = Today;
        (DateOnly StartsOn, DateOnly EndsOn, string? Cancelled)? period = subscription switch
        {
            "none" => null,
            "expired" => (today.AddDays(-200), today.AddDays(-15), null),
            "cancelled" => (today.AddDays(-30), today.AddDays(150), "Association stopped running competitions"),
            "scheduled" => (today.AddDays(1), today.AddDays(180), null),
            _ => throw new ArgumentOutOfRangeException(nameof(subscription)),
        };
        if (period is not { } p)
        {
            return (kmsa, null);
        }

        await TenantTestData.SavePeriodAsync(App.Services, kmsa, p.StartsOn, p.EndsOn, Ct, p.Cancelled);
        var status = subscription switch
        {
            "expired" => SubscriptionStatus.Expired,
            "cancelled" => SubscriptionStatus.Cancelled,
            _ => SubscriptionStatus.Scheduled,
        };
        return (kmsa, new TenantAccessPeriod(status, p.StartsOn, p.EndsOn, p.EndsOn.AddDays(14)));
    }

    private async Task<Guid> TenantAsync(TenantStatus status)
    {
        App.Authorization.UseIdentityRoles();
        return await TenantTestData.SaveTenantAsync(App.Services, "kmsa", status, Ct);
    }

    private static Task<HttpResponseMessage> PatchSettingsAsync(HttpClient client) => client.PatchAsync(
        _settingsPath, new StringContent("""{"startingSoonMinutes":45}""", Encoding.UTF8, "application/json"), Ct);

    private static async Task<TenantAccessResponse> ReadAccessAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TenantAccessResponse>(Json, Ct)).ShouldNotBeNull();
}
