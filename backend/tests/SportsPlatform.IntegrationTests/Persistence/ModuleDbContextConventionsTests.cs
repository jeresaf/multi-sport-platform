using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

public sealed class ModuleDbContextConventionsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _registrar = new("0199f2a1-5c00-7000-8000-00000000a001");

    [Fact]
    public async Task Tables_and_columns_are_snake_case_in_the_module_schema()
    {
        using var scope = App.Services.CreateScope();
        var context = Context(scope);

        var columns = await context.Database.SqlQueryRaw<string>(
                "SELECT column_name::text AS \"Value\" FROM information_schema.columns " +
                "WHERE table_schema = 'conventions_test' AND table_name = 'convention_probes' ORDER BY ordinal_position")
            .ToListAsync(Ct);

        // xmin is PostgreSQL's system column, so it is never created as a real column.
        columns.ShouldBe(["id", "venue_name", "created_at", "created_by", "updated_at", "updated_by"]);
    }

    [Fact]
    public async Task New_ids_are_UUIDv7()
    {
        var probe = new ConventionProbe("Bukoto Community Stadium");
        await SaveNewAsync(probe);

        using var scope = App.Services.CreateScope();
        (await Context(scope).ConventionProbes.Select(p => p.Id).SingleAsync(Ct)).Version.ShouldBe(7);
    }

    [Fact]
    public async Task UUIDv7_ids_sort_by_creation_time_in_PostgreSQL()
    {
        // One ID per minute, saved in shuffled order: the database order must still be the time order.
        var start = App.Clock.GetUtcNow();
        var ids = Enumerable.Range(0, 5).Select(i => Guid.CreateVersion7(start.AddMinutes(i))).ToList();
        foreach (var index in new[] { 3, 0, 4, 1, 2 })
        {
            await SaveNewAsync(new ConventionProbe(ids[index], $"Kisaasi Recreation Ground, pitch {index + 1}"));
        }

        using var scope = App.Services.CreateScope();
        var sorted = await Context(scope).ConventionProbes.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(Ct);

        sorted.ShouldBe(ids);
    }

    [Fact]
    public async Task Audit_columns_record_who_and_when_and_created_values_never_change()
    {
        var createdAt = App.Clock.GetUtcNow();
        var probe = new ConventionProbe("Bukoto Community Stadium");
        await SaveNewAsync(probe);

        App.Clock.Advance(TimeSpan.FromMinutes(5));
        using (var scope = App.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(new UserActor(_registrar));
            var context = Context(scope);
            var stored = await context.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            stored.Rename("Seeta Hill Sports Field");
            await context.SaveChangesAsync(Ct);
        }

        using var checkScope = App.Services.CreateScope();
        var audit = await Context(checkScope).ConventionProbes
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
        audit.UpdatedBy.ShouldBe(_registrar);
    }

    [Fact]
    public async Task A_lost_update_returns_the_modules_concurrency_conflict_error()
    {
        var probe = new ConventionProbe("Bukoto Community Stadium");
        await SaveNewAsync(probe);

        using (var firstScope = App.Services.CreateScope())
        using (var secondScope = App.Services.CreateScope())
        {
            var first = Context(firstScope);
            var second = Context(secondScope);
            var firstCopy = await first.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            var secondCopy = await second.ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct);

            secondCopy.Rename("Seeta Hill Sports Field");
            (await second.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();

            firstCopy.Rename("Kisaasi Recreation Ground");
            var result = await first.SaveChangesResultAsync(Ct);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("TEST_CONCURRENCY_CONFLICT");
        }

        using var checkScope = App.Services.CreateScope();
        (await Context(checkScope).ConventionProbes.SingleAsync(p => p.Id == probe.Id, Ct))
            .VenueName.ShouldBe("Seeta Hill Sports Field");
    }

    [Fact]
    public async Task Saving_a_new_entity_without_an_id_throws()
    {
        using var scope = App.Services.CreateScope();
        var context = Context(scope);
        var probe = (ConventionProbe)Activator.CreateInstance(typeof(ConventionProbe), nonPublic: true)!;
        context.ConventionProbes.Add(probe);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));

        exception.Message.ShouldContain("has no ID");
    }

    private static ConventionsTestDbContext Context(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();

    private async Task SaveNewAsync(ConventionProbe probe)
    {
        using var scope = App.Services.CreateScope();
        var context = Context(scope);
        context.ConventionProbes.Add(probe);
        await context.SaveChangesAsync(Ct);
    }
}
