using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The post-migration step that gives tenants created before S02-T3b their platform subdomain, with the configured
/// base domain (owner, 2026-10-19).
/// </summary>
public sealed class BackfillPlatformSubdomainsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Tenants_without_a_domain_get_their_platform_subdomain_once()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: false, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);

        (await StepAsync(App.Services)).ShouldBe(1);
        (await StepAsync(App.Services)).ShouldBe(0);

        var domains = await TenantTestData.DomainsAsync(App.Services, Ct);
        domains.Select(d => d.Host).ShouldBe(["elssc.localhost", "kmsa.localhost"]);
        domains.Single(d => d.Host == "kmsa.localhost").ShouldSatisfyAllConditions(
            d => d.TenantId.ShouldBe(kmsa),
            d => d.Type.ShouldBe(TenantDomainType.PlatformSubdomain),
            d => d.IsPrimary.ShouldBeTrue(),
            d => d.Status.ShouldBe(TenantDomainStatus.Active));
    }

    [Fact]
    public async Task The_migration_runner_runs_the_step()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: false, Ct);

        await App.Services.GetRequiredService<MigrationRunner>().RunAsync(Ct);

        (await TenantTestData.DomainsAsync(App.Services, Ct)).ShouldHaveSingleItem().Host.ShouldBe("kmsa.localhost");
    }

    [Fact]
    public async Task The_configured_base_domain_is_used()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: false, Ct);
        await using var app = await PublicTestApp.StartAsync(Fixture, TestEnvironment.Name, Ct, ("Platform:BaseDomain", "sports.test"));

        (await StepAsync(app.Services)).ShouldBe(1);

        (await TenantTestData.DomainsAsync(App.Services, Ct)).ShouldHaveSingleItem().Host.ShouldBe("kmsa.sports.test");
    }

    [Theory]
    [InlineData("Sports.Test")]
    [InlineData("localhost:3000")]
    [InlineData("sports..test")]
    public async Task An_invalid_base_domain_stops_the_app(string baseDomain)
    {
        await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await using var app = await PublicTestApp.StartAsync(Fixture, TestEnvironment.Name, Ct, ("Platform:BaseDomain", baseDomain));
        });
    }

    private static Task<int> StepAsync(IServiceProvider services) =>
        services.GetServices<IPostMigrationStep>().Single(s => s.Name == "tenancy.platform-subdomains").RunAsync(TestContext.Current.CancellationToken);
}
