using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>Allowed and denied for every Tenancy action (each endpoint asks the evaluator for its own action).</summary>
public sealed class TenantActionAuthorizationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public enum Endpoint
    {
        Create,
        List,
        Get,
        Update,
        Activate,
        Suspend,
        Restore,
        Archive,
        Unarchive,
        GetSettings,
        UpdateSettings,
        GetAccess,
        ListPlans,
        GetSubscription,
        AssignPeriod,
        RenewSubscription,
        ChangePlan,
        CancelSubscription,
    }

    private static readonly ActionDefinition[] _actions =
    [
        TenancyActions.TenantCreate, TenancyActions.TenantRead, TenancyActions.TenantUpdate, TenancyActions.TenantActivate,
        TenancyActions.TenantSuspend, TenancyActions.TenantRestore, TenancyActions.TenantArchive, TenancyActions.TenantUnarchive,
        TenancyActions.TenantSettingsRead, TenancyActions.TenantSettingsUpdate, TenancyActions.TenantAccessRead,
        TenancyActions.PlanRead, TenancyActions.SubscriptionRead, TenancyActions.SubscriptionAssign, TenancyActions.SubscriptionRenew,
        TenancyActions.SubscriptionChangePlan, TenancyActions.SubscriptionCancel,
    ];

    [Fact]
    public void Every_Tenancy_action_is_covered() =>
        typeof(TenancyActions).GetFields().Select(f => ((ActionDefinition)f.GetValue(null)!).Code.Value)
            .ShouldBe(_actions.Select(a => a.Code.Value), ignoreOrder: true);

    [Theory]
    [InlineData(Endpoint.Create, "tenant.create", HttpStatusCode.Created)]
    [InlineData(Endpoint.List, "tenant.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.Get, "tenant.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.Update, "tenant.update", HttpStatusCode.OK)]
    [InlineData(Endpoint.Activate, "tenant.activate", HttpStatusCode.OK)]
    [InlineData(Endpoint.Suspend, "tenant.suspend", HttpStatusCode.OK)]
    [InlineData(Endpoint.Restore, "tenant.restore", HttpStatusCode.OK)]
    [InlineData(Endpoint.Archive, "tenant.archive", HttpStatusCode.OK)]
    [InlineData(Endpoint.Unarchive, "tenant.unarchive", HttpStatusCode.OK)]
    [InlineData(Endpoint.GetSettings, "tenant.settings.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.UpdateSettings, "tenant.settings.update", HttpStatusCode.OK)]
    [InlineData(Endpoint.GetAccess, "tenant.access.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.ListPlans, "plan.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.GetSubscription, "subscription.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.AssignPeriod, "subscription.assign", HttpStatusCode.Created)]

    // Past authorization: the tenant has no period to renew, change or cancel.
    [InlineData(Endpoint.RenewSubscription, "subscription.renew", HttpStatusCode.Conflict)]
    [InlineData(Endpoint.ChangePlan, "subscription.change_plan", HttpStatusCode.Conflict)]
    [InlineData(Endpoint.CancelSubscription, "subscription.cancel", HttpStatusCode.Conflict)]
    public async Task Allowed(Endpoint endpoint, string action, HttpStatusCode expected)
    {
        // The subscription commands start from no period (assign adds the first); everything else from test data's.
        var withSubscription = endpoint is not (Endpoint.AssignPeriod or Endpoint.RenewSubscription or Endpoint.ChangePlan or Endpoint.CancelSubscription);
        var tenantId = await TenantTestData.SaveTenantAsync(
            App.Services, "elssc", StatusFor(endpoint), publicEnabled: false, withDomain: true, withSubscription, Ct);
        App.Authorization.Allow(Action(action));

        using var response = await SendAsync(endpoint, tenantId);

        response.StatusCode.ShouldBe(expected);
        App.Authorization.Requests.ShouldHaveSingleItem().Action.Code.Value.ShouldBe(action);
    }

    [Theory]
    [InlineData(Endpoint.Create, "tenant.read")]
    [InlineData(Endpoint.List, "tenant.create")]
    [InlineData(Endpoint.Get, "tenant.update")]
    [InlineData(Endpoint.Update, "tenant.read")]
    [InlineData(Endpoint.Activate, "tenant.restore")]
    [InlineData(Endpoint.Suspend, "tenant.archive")]
    [InlineData(Endpoint.Restore, "tenant.activate")]
    [InlineData(Endpoint.Archive, "tenant.suspend")]
    [InlineData(Endpoint.Unarchive, "tenant.restore")]
    [InlineData(Endpoint.GetSettings, "tenant.settings.update")]
    [InlineData(Endpoint.UpdateSettings, "tenant.settings.read")]
    [InlineData(Endpoint.GetAccess, "tenant.settings.read")]
    [InlineData(Endpoint.ListPlans, "tenant.read")]
    [InlineData(Endpoint.GetSubscription, "plan.read")]
    [InlineData(Endpoint.AssignPeriod, "subscription.read")]
    [InlineData(Endpoint.RenewSubscription, "subscription.assign")]
    [InlineData(Endpoint.ChangePlan, "subscription.renew")]
    [InlineData(Endpoint.CancelSubscription, "subscription.change_plan")]
    public async Task Denied_without_its_own_action(Endpoint endpoint, string otherAction)
    {
        var status = StatusFor(endpoint);
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", status, Ct);
        App.Authorization.Allow(Action(otherAction));

        using var response = await SendAsync(endpoint, tenantId);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(1);
        (await TenantTestData.TenantAsync(App.Services, tenantId, Ct)).Status.ShouldBe(status);
        (await TenantTestData.SettingsAsync(App.Services, tenantId, Ct)).StartingSoonMinutes.ShouldBe(30);
    }

    private static ActionDefinition Action(string code) => _actions.Single(a => a.Code.Value == code);

    /// <summary>A status from which the endpoint's transition is allowed.</summary>
    private static TenantStatus StatusFor(Endpoint endpoint) => endpoint switch
    {
        Endpoint.Activate => TenantStatus.Provisioning,
        Endpoint.Restore => TenantStatus.Suspended,
        Endpoint.Unarchive => TenantStatus.Archived,
        _ => TenantStatus.Active,
    };

    private async Task<HttpResponseMessage> SendAsync(Endpoint endpoint, Guid tenantId)
    {
        // A signed-in user (anonymous is 401, S03 D4); tenant actions: a member of the tenant, named in X-Tenant-Id.
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId, tenantId);
        if (endpoint is Endpoint.GetSettings or Endpoint.UpdateSettings or Endpoint.GetAccess)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        }

        var tenant = $"{TenantTestData.TenantsPath}/{tenantId}";
        var reason = new { reason = "Council closed for the school holidays" };
        return endpoint switch
        {
            Endpoint.Create => await client.PostAsJsonAsync(
                new Uri(TenantTestData.TenantsPath, UriKind.Relative), TenantTestData.CreateBody(), Ct),
            Endpoint.List => await client.GetAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), Ct),
            Endpoint.Get => await client.GetAsync(new Uri(tenant, UriKind.Relative), Ct),
            Endpoint.Update => await client.PatchAsJsonAsync(
                new Uri(tenant, UriKind.Relative), new { displayName = "Eastern Lakes Schools" }, Ct),
            Endpoint.Activate => await client.PostAsync(new Uri($"{tenant}/activate", UriKind.Relative), null, Ct),
            Endpoint.Restore => await client.PostAsync(new Uri($"{tenant}/restore", UriKind.Relative), null, Ct),
            Endpoint.GetAccess => await client.GetAsync(new Uri("/api/admin/v1/tenant/access", UriKind.Relative), Ct),
            Endpoint.ListPlans => await client.GetAsync(new Uri("/api/platform/v1/plans", UriKind.Relative), Ct),
            Endpoint.GetSubscription => await client.GetAsync(new Uri($"{tenant}/subscription", UriKind.Relative), Ct),
            Endpoint.AssignPeriod => await client.PostAsJsonAsync(
                new Uri($"{tenant}/subscription/periods", UriKind.Relative),
                new { planCode = "STANDARD", kind = "PAID", startsOn = "2026-10-01", endsOn = "2027-09-30" },
                Ct),
            Endpoint.RenewSubscription => await client.PostAsJsonAsync(
                new Uri($"{tenant}/subscription/renew", UriKind.Relative), new { endsOn = "2027-09-30" }, Ct),
            Endpoint.ChangePlan => await client.PostAsJsonAsync(
                new Uri($"{tenant}/subscription/change-plan", UriKind.Relative), new { planCode = "PRO" }, Ct),
            Endpoint.CancelSubscription => await client.PostAsJsonAsync(new Uri($"{tenant}/subscription/cancel", UriKind.Relative), reason, Ct),
            Endpoint.GetSettings => await client.GetAsync(new Uri(TenantSettingsTests.SettingsPath, UriKind.Relative), Ct),
            Endpoint.UpdateSettings => await client.PatchAsJsonAsync(
                new Uri(TenantSettingsTests.SettingsPath, UriKind.Relative), new { startingSoonMinutes = 45 }, Ct),
            _ => await client.PostAsJsonAsync(
                new Uri($"{tenant}/{endpoint.ToString().ToLowerInvariant()}", UriKind.Relative), reason, Ct),
        };
    }
}
