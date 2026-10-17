using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

public sealed class ModuleDbContextConventionsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Tables_and_columns_are_snake_case_in_the_module_schema()
    {
        await using var context = NewContext(out var scope);
        using (scope)
        {
            var columns = await context.Database.SqlQueryRaw<string>(
                    "SELECT column_name::text AS \"Value\" FROM information_schema.columns " +
                    "WHERE table_schema = 'conventions_test' AND table_name = 'convention_probes' ORDER BY ordinal_position")
                .ToListAsync(Ct);

            // xmin is PostgreSQL's system column, so it is never created as a real column.
            columns.ShouldBe(["id", "venue_name", "created_at", "created_by", "updated_at", "updated_by"]);
        }
    }

    [Fact]
    public async Task Ids_are_UUIDv7_and_sort_in_creation_order()
    {
        var created = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var probe = new ConventionProbe($"Kisaasi Recreation Ground, pitch {i + 1}");
            created.Add(probe.Id);
            await SaveNewAsync(probe);
        }

        await using var context = NewContext(out var scope);
        using (scope)
        {
            var sorted = await context.ConventionProbes.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(Ct);

            sorted.ShouldAllBe(id => id.Version == 7);
            // PostgreSQL orders uuids bytewise, so the millisecond prefix sorts like creation time.
            sorted.Select(UnixMilliseconds).ShouldBe(created.Select(UnixMilliseconds));
        }
    }

    [Fact]
    public async Task Audit_columns_record_the_actor_and_the_clock_and_created_values_never_change()
    {
        var createdAt = App.Clock.GetUtcNow();
        var probe = new ConventionProbe("Bukoto Community Stadium");
        await SaveNewAsync(probe);

        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await using (var context = NewContext(out var scope))
        using (scope)
        {
            var stored = await context.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            stored.Rename("Seeta Hill Sports Field");
            await context.SaveChangesAsync(Ct);
        }

        await using var check = NewContext(out var checkScope);
        using (checkScope)
        {
            var audit = await check.ConventionProbes
                .Where(p => p.Id == probe.Id)
                .Select(p => new
                {
                    CreatedAt = EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt),
                    CreatedBy = EF.Property<Guid>(p, AuditColumns.CreatedBy),
                    UpdatedAt = EF.Property<DateTimeOffset>(p, AuditColumns.UpdatedAt),
                    UpdatedBy = EF.Property<Guid>(p, AuditColumns.UpdatedBy),
                })
                .SingleAsync(Ct);

            audit.CreatedAt.ShouldBe(createdAt);
            audit.CreatedBy.ShouldBe(SystemActor.Id);
            audit.UpdatedAt.ShouldBe(createdAt.AddMinutes(5));
            audit.UpdatedBy.ShouldBe(SystemActor.Id);
        }
    }

    [Fact]
    public async Task A_lost_update_returns_the_modules_concurrency_conflict_error()
    {
        var probe = new ConventionProbe("Bukoto Community Stadium");
        await SaveNewAsync(probe);

        await using var first = NewContext(out var firstScope);
        await using var second = NewContext(out var secondScope);
        using (firstScope)
        using (secondScope)
        {
            var firstCopy = await first.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            var secondCopy = await second.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);

            secondCopy.Rename("Seeta Hill Sports Field");
            (await second.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();

            firstCopy.Rename("Kisaasi Recreation Ground");
            var result = await first.SaveChangesResultAsync(Ct);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("TEST_CONCURRENCY_CONFLICT");
        }

        await using var check = NewContext(out var checkScope);
        using (checkScope)
        {
            (await check.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct)).VenueName.ShouldBe("Seeta Hill Sports Field");
        }
    }

    [Fact]
    public async Task Saving_a_new_entity_without_an_id_throws()
    {
        await using var context = NewContext(out var scope);
        using (scope)
        {
            var probe = (ConventionProbe)Activator.CreateInstance(typeof(ConventionProbe), nonPublic: true)!;
            context.ConventionProbes.Add(probe);

            await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }
    }

    private ConventionsTestDbContext NewContext(out IServiceScope scope)
    {
        scope = App.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
    }

    private async Task SaveNewAsync(ConventionProbe probe)
    {
        await using var context = NewContext(out var scope);
        using (scope)
        {
            context.ConventionProbes.Add(probe);
            await context.SaveChangesAsync(Ct);
        }
    }

    private static long UnixMilliseconds(Guid id) => Convert.ToInt64(id.ToString("N")[..12], 16);
}
