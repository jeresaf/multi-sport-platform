using Microsoft.EntityFrameworkCore;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Each Registry aggregate answers its own conflict (S07 D2, the S06 tech-debt item #95 Q1): two saves from the same
/// version, and the second is refused with the code of the root whose version failed.
/// </summary>
public sealed class RegistryConcurrencyConflictTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // T1 acceptance: a conflict on a venue answers VENUE_CONCURRENCY_CONFLICT, on a person still PERSON_….
    [Fact]
    public async Task A_lost_update_answers_the_conflict_of_its_aggregate()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var person = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        (await LoseAnUpdateAsync<Venue>(kmsa, venue.Id, nameof(Venue.Capacity), 1_200, 1_500)).ShouldBe("VENUE_CONCURRENCY_CONFLICT");
        (await LoseAnUpdateAsync<Person>(kmsa, person.Id, nameof(Person.HeightCm), 180, 181)).ShouldBe("PERSON_CONCURRENCY_CONFLICT");
        (await LoseAnUpdateAsync<Organisation>(kmsa, organisation.Id, nameof(Organisation.FoundedYear), 1998, 1999))
            .ShouldBe("ORGANISATION_CONCURRENCY_CONFLICT");
    }

    // D2: a root added to the module (organisations, teams) can't fall back to the person's code unnoticed.
    [Fact]
    public void Every_aggregate_root_of_the_module_has_its_own_conflict_code()
    {
        using var scope = ScopeFor(App.Services, Kmsa, out var context);

        var roots = context.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => type.IsSubclassOf(typeof(BuildingBlocks.Domain.AggregateRoot)))
            .ToList();

        roots.ShouldNotBeEmpty();
        roots.ShouldAllBe(type => RegistryConcurrencyConflicts.For(type) != null);
    }

    // Two scopes read the same version; the first saves, the second's save answers the code.
    private async Task<string?> LoseAnUpdateAsync<TRoot>(Guid tenantId, Guid id, string property, object first, object second)
        where TRoot : BuildingBlocks.Domain.AggregateRoot
    {
        await using var winnerScope = ScopeFor(App.Services, tenantId, out var winner);
        await using var loserScope = ScopeFor(App.Services, tenantId, out var loser);
        var won = await winner.Set<TRoot>().SingleAsync(root => root.Id == id, Ct);
        var lost = await loser.Set<TRoot>().SingleAsync(root => root.Id == id, Ct);

        winner.Entry(won).Property(property).CurrentValue = first;
        (await winner.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();
        loser.Entry(lost).Property(property).CurrentValue = second;
        var result = await loser.SaveChangesResultAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        return result.Error.Code;
    }
}
