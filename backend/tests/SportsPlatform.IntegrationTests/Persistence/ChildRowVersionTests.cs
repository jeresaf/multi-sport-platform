using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

// S06 D11: a change to only a child row moves its aggregate root's version (xmin), in every module's context.
public sealed class ChildRowVersionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenant = new("0192a1b2-0000-7000-8000-0000000000d1");

    [Fact]
    public async Task Two_concurrent_changes_to_different_children_of_one_aggregate_conflict()
    {
        var probe = await SaveProbeAsync("Kisaasi Recreation Ground");

        await using var firstScope = ScopeFor(out var first);
        await using var secondScope = ScopeFor(out var second);
        var firstCopy = await first.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
        var secondCopy = await second.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);

        second.Add(new TenantProbeNote(secondCopy.Id, "Pitch mown on Thursdays"));
        (await second.SaveChangesResultAsync(Ct)).IsSuccess.ShouldBeTrue();

        first.Add(new TenantProbeNote(firstCopy.Id, "Scoreboard repaired"));
        var result = await first.SaveChangesResultAsync(Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TEST_CONCURRENCY_CONFLICT");
        (await CountNotesAsync(probe.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task A_changed_child_row_moves_the_roots_version_and_updated_at()
    {
        var probe = await SaveProbeAsync("Metro Indoor Arena, Naguru");
        Guid noteId;
        await using (var scope = ScopeFor(out var context))
        {
            var loaded = await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            var note = new TenantProbeNote(loaded.Id, "Court two closed");
            context.Add(note);
            await context.SaveChangesAsync(Ct);
            noteId = note.Id;
        }

        var before = await VersionAsync(probe.Id);
        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            var note = await context.TenantProbeNotes.SingleAsync(n => n.Id == noteId, Ct);
            context.Entry(note).Property(n => n.Text).CurrentValue = "Court two open again";
            await context.SaveChangesAsync(Ct);
        }

        var after = await VersionAsync(probe.Id);
        after.Version.ShouldNotBe(before.Version);
        after.UpdatedAt.ShouldBe(before.UpdatedAt + TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task A_deleted_child_row_moves_the_roots_version()
    {
        var probe = await SaveProbeAsync("Seeta Hill Sports Field");
        var noteId = await SaveNoteAsync(probe.Id, "Boundary rope missing");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            context.Remove(await context.TenantProbeNotes.SingleAsync(n => n.Id == noteId, Ct));
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(probe.Id)).Version.ShouldNotBe(before.Version);
        (await CountNotesAsync(probe.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task A_child_added_by_a_domain_event_subscriber_moves_the_roots_version()
    {
        var probe = await SaveProbeAsync("Ggaba Lakeside Rugby Ground");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            var loaded = await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            loaded.RequestNote("Posts padded");
            await context.SaveChangesAsync(Ct);
        }

        (await CountNotesAsync(probe.Id)).ShouldBe(1);
        (await VersionAsync(probe.Id)).Version.ShouldNotBe(before.Version);
    }

    [Fact]
    public async Task A_row_two_levels_below_the_root_moves_the_roots_version()
    {
        var probe = await SaveProbeAsync("Metro Indoor Arena, Naguru");
        var noteId = await SaveNoteAsync(probe.Id, "Scorers' table moved");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            await context.TenantProbeNotes.SingleAsync(n => n.Id == noteId, Ct);
            context.Add(new TenantProbeNoteMark(noteId, "Checked"));
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(probe.Id)).Version.ShouldNotBe(before.Version);
    }

    [Fact]
    public async Task A_child_moved_to_another_root_moves_both_roots_versions()
    {
        var from = await SaveProbeAsync("Bukoto Community Stadium");
        var to = await SaveProbeAsync("Kisaasi Recreation Ground");
        var noteId = await SaveNoteAsync(from.Id, "Spare corner flags");
        var fromBefore = await VersionAsync(from.Id);
        var toBefore = await VersionAsync(to.Id);

        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.Where(p => p.Id == from.Id || p.Id == to.Id).ToListAsync(Ct);
            var note = await context.TenantProbeNotes.SingleAsync(n => n.Id == noteId, Ct);
            note.MoveTo(to.Id);
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(from.Id)).Version.ShouldNotBe(fromBefore.Version);
        (await VersionAsync(to.Id)).Version.ShouldNotBe(toBefore.Version);
    }

    [Fact]
    public async Task A_child_moved_and_deleted_in_one_save_moves_only_the_version_of_the_root_it_was_stored_under()
    {
        var from = await SaveProbeAsync("Seeta Hill Sports Field");
        var to = await SaveProbeAsync("Ggaba Lakeside Rugby Ground");
        var noteId = await SaveNoteAsync(from.Id, "Old scoreboard");
        var fromBefore = await VersionAsync(from.Id);
        var toBefore = await VersionAsync(to.Id);

        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.Where(p => p.Id == from.Id || p.Id == to.Id).ToListAsync(Ct);
            var note = await context.TenantProbeNotes.SingleAsync(n => n.Id == noteId, Ct);
            note.MoveTo(to.Id);
            context.Remove(note);
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(from.Id)).Version.ShouldNotBe(fromBefore.Version);
        (await VersionAsync(to.Id)).ShouldBe(toBefore);
        (await CountNotesAsync(from.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task A_child_saved_without_its_root_loaded_leaves_the_root_alone()
    {
        var probe = await SaveProbeAsync("Ggaba Lakeside Rugby Ground");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            context.Add(new TenantProbeNote(probe.Id, "Goalposts painted"));
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(probe.Id)).ShouldBe(before);
    }

    [Fact]
    public async Task A_save_that_changes_nothing_below_a_loaded_root_leaves_it_alone()
    {
        var probe = await SaveProbeAsync("Bukoto Community Stadium");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(probe.Id)).ShouldBe(before);
    }

    [Fact]
    public async Task Marking_a_loaded_root_changed_moves_its_version()
    {
        var probe = await SaveProbeAsync("Kisaasi Recreation Ground");
        var before = await VersionAsync(probe.Id);

        await using (var scope = ScopeFor(out var context))
        {
            var loaded = await context.TenantProbes.SingleAsync(p => p.Id == probe.Id, Ct);
            context.MarkChanged(loaded);
            await context.SaveChangesAsync(Ct);
        }

        (await VersionAsync(probe.Id)).Version.ShouldNotBe(before.Version);
    }

    [Fact]
    public async Task Marking_a_root_that_is_not_loaded_is_a_bug()
    {
        var probe = await SaveProbeAsync("Bukoto Community Stadium");

        await using var scope = ScopeFor(out var context);

        Should.Throw<InvalidOperationException>(() => context.MarkChanged(probe)).Message.ShouldContain("not loaded");
    }

    private async Task<TenantProbe> SaveProbeAsync(string name)
    {
        await using var scope = ScopeFor(out var context);
        var probe = new TenantProbe(name);
        context.Add(probe);
        await context.SaveChangesAsync(Ct);
        return probe;
    }

    private async Task<Guid> SaveNoteAsync(Guid probeId, string text)
    {
        await using var scope = ScopeFor(out var context);
        var note = new TenantProbeNote(probeId, text);
        context.Add(note);
        await context.SaveChangesAsync(Ct);
        return note.Id;
    }

    private async Task<(uint Version, DateTimeOffset UpdatedAt)> VersionAsync(Guid probeId)
    {
        await using var scope = ScopeFor(out var context);
        return await context.TenantProbes
            .Where(p => p.Id == probeId)
            .Select(p => new ValueTuple<uint, DateTimeOffset>(
                EF.Property<uint>(p, AuditColumns.Version), EF.Property<DateTimeOffset>(p, AuditColumns.UpdatedAt)))
            .SingleAsync(Ct);
    }

    private async Task<int> CountNotesAsync(Guid probeId)
    {
        await using var scope = ScopeFor(out var context);
        return await context.TenantProbeNotes.CountAsync(n => n.ProbeId == probeId, Ct);
    }

    private AsyncServiceScope ScopeFor(out ConventionsTestDbContext context)
    {
        var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
        context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
        return scope;
    }
}
