using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Host;
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
    [InlineData("a234567890123456789012345678901234567890123456789012345678901.b234567890123456789012345678901234567890123456789012345678901.c234567890123456789012345678901234567890123456789012345678901.d2345678901234567890123456789012345678901234567890123456")]
    public async Task An_invalid_base_domain_stops_the_app(string baseDomain)
    {
        await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await using var app = await PublicTestApp.StartAsync(Fixture, TestEnvironment.Name, Ct, ("Platform:BaseDomain", baseDomain));
        });
    }

    [Fact]
    public async Task A_malformed_proxy_address_stops_the_app() =>
        await Should.ThrowAsync<FormatException>(async () =>
        {
            await using var app = await PublicTestApp.StartAsync(Fixture, TestEnvironment.Name, Ct, ("ForwardedHeaders:KnownProxies:1", "10.20.0.300"));
        });

    // #35 R1: a missing base domain must not give tenants permanent <slug>.localhost domains outside development.
    [Theory]
    [InlineData("Staging", null)]
    [InlineData("Production", null)]
    [InlineData("Production", "localhost")]
    [InlineData("Staging", "staging.localhost")]
    public async Task Outside_development_the_base_domain_must_be_configured(string environment, string? baseDomain)
    {
        var settings = baseDomain is null ? [] : new[] { ("Platform:BaseDomain", baseDomain) };

        await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await using var app = await PublicTestApp.StartAsync(Fixture, environment, Ct, settings);
        });
    }

    [Fact]
    public async Task Outside_development_the_backfill_refuses_without_a_base_domain()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: false, Ct);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(Fixture.AppSettings);
        SportsPlatformApp.AddServices(builder);
        await using var app = builder.Build(); // built, not started, like the CLI's migrate

        await Should.ThrowAsync<OptionsValidationException>(() => StepAsync(app.Services));
        (await TenantTestData.DomainsAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Staging", "staging.sports.test")]
    [InlineData("Production", "sports.test")]
    public async Task Outside_development_a_configured_base_domain_starts(string environment, string baseDomain)
    {
        await using var app = await PublicTestApp.StartAsync(Fixture, environment, Ct, ("Platform:BaseDomain", baseDomain));
    }

    private static Task<int> StepAsync(IServiceProvider services) =>
        services.GetServices<IPostMigrationStep>().Single(s => s.Name == "tenancy.platform-subdomains").RunAsync(TestContext.Current.CancellationToken);
}
