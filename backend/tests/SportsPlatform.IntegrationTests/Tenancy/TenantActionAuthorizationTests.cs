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
    }

    [Theory]
    [InlineData(Endpoint.Create, "tenant.create", HttpStatusCode.Created)]
    [InlineData(Endpoint.List, "tenant.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.Get, "tenant.read", HttpStatusCode.OK)]
    [InlineData(Endpoint.Update, "tenant.update", HttpStatusCode.OK)]
    public async Task Allowed(Endpoint endpoint, string action, HttpStatusCode expected)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
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
    public async Task Denied_without_its_own_action(Endpoint endpoint, string otherAction)
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        App.Authorization.Allow(Action(otherAction));

        using var response = await SendAsync(endpoint, tenantId);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(1);
    }

    private static ActionDefinition Action(string code) =>
        new[] { TenancyActions.TenantCreate, TenancyActions.TenantRead, TenancyActions.TenantUpdate }.Single(a => a.Code.Value == code);

    private async Task<HttpResponseMessage> SendAsync(Endpoint endpoint, Guid tenantId)
    {
        using var client = App.CreateClient();
        var tenant = new Uri($"{TenantTestData.TenantsPath}/{tenantId}", UriKind.Relative);
        return endpoint switch
        {
            Endpoint.Create => await client.PostAsJsonAsync(
                new Uri(TenantTestData.TenantsPath, UriKind.Relative), TenantTestData.CreateBody(), Ct),
            Endpoint.List => await client.GetAsync(new Uri(TenantTestData.TenantsPath, UriKind.Relative), Ct),
            Endpoint.Get => await client.GetAsync(tenant, Ct),
            _ => await client.PatchAsJsonAsync(tenant, new { displayName = "Eastern Lakes Schools" }, Ct),
        };
    }
}
