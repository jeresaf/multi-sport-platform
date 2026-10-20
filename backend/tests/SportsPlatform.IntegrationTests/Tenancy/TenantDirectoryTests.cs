using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary><c>ITenantDirectory.GetManyAsync</c>: several tenants by ID, before any tenant is set (the global table).</summary>
public sealed class TenantDirectoryTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Get_many_returns_the_tenants_found_in_every_status_and_leaves_unknown_IDs_out()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var acacia = await TenantTestData.SaveTenantAsync(App.Services, "acacia-schools", TenantStatus.Archived, Ct);
        var jacaranda = await TenantTestData.SaveTenantAsync(App.Services, "jacaranda-games", TenantStatus.Suspended, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        await using var scope = App.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();

        var tenants = await directory.GetManyAsync([kmsa, acacia, jacaranda, Guid.CreateVersion7()], Ct);

        tenants.OrderBy(t => t.Slug, StringComparer.Ordinal).ShouldBe([
            new TenantSummary(acacia, "acacia-schools", TenantStatus.Archived, "acacia-schools Sports"),
            new TenantSummary(jacaranda, "jacaranda-games", TenantStatus.Suspended, "jacaranda-games Sports"),
            new TenantSummary(kmsa, "kmsa", TenantStatus.Active, "kmsa Sports"),
        ]);
    }

    [Fact]
    public async Task Get_many_of_no_IDs_is_empty()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await using var scope = App.Services.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<ITenantDirectory>().GetManyAsync([], Ct)).ShouldBeEmpty();
    }
}
