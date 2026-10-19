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
    }

    private static readonly ActionDefinition[] _actions =
    [
        TenancyActions.TenantCreate, TenancyActions.TenantRead, TenancyActions.TenantUpdate, TenancyActions.TenantActivate,
        TenancyActions.TenantSuspend, TenancyActions.TenantRestore, TenancyActions.TenantArchive, TenancyActions.TenantUnarchive,
        TenancyActions.TenantSettingsRead, TenancyActions.TenantSettingsUpdate,
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
    public async Task Allowed(Endpoint endpoint, string action, HttpStatusCode expected)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", StatusFor(endpoint), Ct);
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
        using var client = App.CreateClient();
        if (endpoint is Endpoint.GetSettings or Endpoint.UpdateSettings)
        {
            // Tenant actions: a member of the tenant, named in X-Tenant-Id.
            client.AsUser(TenantTestData.StaffId, tenantId).DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
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
            Endpoint.GetSettings => await client.GetAsync(new Uri(TenantSettingsTests.SettingsPath, UriKind.Relative), Ct),
            Endpoint.UpdateSettings => await client.PatchAsJsonAsync(
                new Uri(TenantSettingsTests.SettingsPath, UriKind.Relative), new { startingSoonMinutes = 45 }, Ct),
            _ => await client.PostAsJsonAsync(
                new Uri($"{tenant}/{endpoint.ToString().ToLowerInvariant()}", UriKind.Relative), reason, Ct),
        };
    }
}
