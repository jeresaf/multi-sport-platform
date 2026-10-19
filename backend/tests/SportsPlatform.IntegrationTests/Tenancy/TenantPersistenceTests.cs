using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The races the handlers rely on the database for (S02-T1a follow-ups): a change saved between another request's read
/// and save, and a slug inserted between another create's check and insert.
/// </summary>
public sealed class TenantPersistenceTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_change_saved_after_this_read_conflicts_on_save()
    {
        var tenantId = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await using var first = App.Services.CreateAsyncScope();
        var firstContext = first.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var mine = await firstContext.Set<Tenant>().SingleAsync(t => t.Id == tenantId, Ct);

        await using (var second = App.Services.CreateAsyncScope())
        {
            var context = second.ServiceProvider.GetRequiredService<TenancyDbContext>();
            var theirs = await context.Set<Tenant>().SingleAsync(t => t.Id == tenantId, Ct);
            theirs.UpdateProfile(theirs.Profile with { DisplayName = "Kampala Metro" }).ShouldBeTrue();
            (await context.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        mine.UpdateProfile(mine.Profile with { DisplayName = "KMSA" }).ShouldBeTrue();
        (await firstContext.SaveChangesResultAsync(Ct)).Error.ShouldBe(TenancyErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task A_duplicate_slug_hits_the_index_the_create_handler_catches()
    {
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Archived, Ct);
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var profile = new TenantProfile("Kampala Metro Sports", "KMSA", "UG");
        var tenant = Tenant.Create("kmsa", profile, SystemActor.Instance, App.Clock.GetUtcNow());
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant.Id); // as the create handler does (D2)
        context.Add(tenant);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        exception.InnerException.ShouldBeOfType<PostgresException>().ShouldSatisfyAllConditions(
            e => e.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation),
            e => e.ConstraintName.ShouldBe("ux_tenants_slug"));
    }
}
