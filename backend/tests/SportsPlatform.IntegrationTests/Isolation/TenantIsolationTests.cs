using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// The isolation harness (docs/architecture/multi-tenancy.md, layer 3): for every tenant-owned entity of every module
/// DbContext, a row of tenant A is invisible to tenant B and can't be changed, deleted, copied into A or referenced
/// from B — in EF (query filter, tenant interceptor) and in PostgreSQL (RLS, composite foreign keys).
/// </summary>
public sealed class TenantIsolationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenantB = new("0192a1b2-0000-7000-8000-0000000000b2");

    public static TheoryData<string> Samples { get; } = [.. IsolationSamples.All.Select(sample => sample.EntityType.Name)];

    [Fact]
    public void Every_tenant_owned_entity_has_a_sample()
    {
        var discovered = TenantOwnedEntityTypes().Select(entityType => entityType.ClrType).ToList();

        discovered.ShouldNotBeEmpty();
        discovered.Where(type => IsolationSamples.All.All(sample => sample.EntityType != type)).Select(type => type.Name)
            .ShouldBeEmpty("tenant-owned entities without an isolation sample");
        IsolationSamples.All.Select(sample => sample.EntityType).Where(type => !discovered.Contains(type)).Select(type => type.Name)
            .ShouldBeEmpty("samples for types that are not tenant-owned entities of a module DbContext");
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Another_tenant_cannot_read_change_delete_copy_or_reference_a_row(string entityName)
    {
        var sample = IsolationSamples.All.Single(s => s.EntityType.Name == entityName);
        var (contextType, entityType) = Locate(sample.EntityType);
        var table = TableName(entityType);
        var (row, tenantA) = await CreateInTenantAAsync(sample, contextType);
        tenantA.ShouldNotBe(_tenantB);

        // Not visible: EF (query filter) and the database (RLS) both show nothing to B; A still sees its row.
        await using (var scope = ScopeFor(_tenantB, contextType, out var context))
        {
            (await context.FindAsync(sample.EntityType, [row.Id], Ct)).ShouldBeNull();
        }

        (await CountAsTenantAsync(tenantA, table, row.Id)).ShouldBe(1);
        (await CountAsTenantAsync(_tenantB, table, row.Id)).ShouldBe(0);

        // Not changed or deleted: the interceptor refuses another tenant's entity, and RLS matches no row.
        foreach (var state in new[] { EntityState.Modified, EntityState.Deleted })
        {
            await using var scope = ScopeFor(_tenantB, contextType, out var context);
            context.Attach(row).State = state;
            await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        foreach (var sql in new[] { $"UPDATE {table} SET tenant_id = tenant_id WHERE id = @id", $"DELETE FROM {table} WHERE id = @id" })
        {
            if (entityType.IsAppendOnly())
            {
                // The runtime roles have no UPDATE or DELETE grant on append-only tables at all.
                (await Should.ThrowAsync<PostgresException>(() => ExecuteAsTenantAsync(_tenantB, sql, row.Id)))
                    .SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
            }
            else
            {
                (await ExecuteAsTenantAsync(_tenantB, sql, row.Id)).ShouldBe(0);
            }
        }

        (await CountAsTenantAsync(tenantA, table, row.Id)).ShouldBe(1);

        // Not copied into A: a write from B with A's tenant ID fails RLS's WITH CHECK.
        var copyOfRow = await RowAsJsonWithNewIdAsync(table, row.Id);
        var copy = await Should.ThrowAsync<PostgresException>(() => ExecuteAsTenantAsync(
            _tenantB, $"INSERT INTO {table} SELECT * FROM jsonb_populate_record(NULL::{table}, @row::jsonb)", row.Id, copyOfRow));
        copy.ShouldSatisfyAllConditions(
            e => e.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege),
            e => e.MessageText.ShouldContain("row-level security"));

        // Not referenced: each tenant-owned foreign key onto this type includes tenant_id, so B can't point at A's row.
        foreach (var foreignKey in entityType.GetReferencingForeignKeys().Where(fk => typeof(ITenantOwned).IsAssignableFrom(fk.DeclaringEntityType.ClrType)))
        {
            var referencing = IsolationSamples.For(foreignKey.DeclaringEntityType.ClrType).Referencing(foreignKey, row.Id)
                ?? throw new InvalidOperationException(
                    $"The {foreignKey.DeclaringEntityType.ClrType.Name} sample must build a row referencing a {entityName}.");
            await using var scope = ScopeFor(_tenantB, contextType, out var context);
            context.Add(referencing);
            var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
            exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        }
    }

    private async Task<(Entity Row, Guid TenantId)> CreateInTenantAAsync(IIsolationSample sample, Type contextType)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var row = sample.Add(context);
        var carried = ((ITenantOwned)row).TenantId;
        var tenantA = carried == Guid.Empty ? new Guid("0192a1b2-0000-7000-8000-0000000000a1") : carried;
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantA);
        await context.SaveChangesAsync(Ct);
        context.ChangeTracker.Clear();
        return (row, tenantA);
    }

    private AsyncServiceScope ScopeFor(Guid tenantId, Type contextType, out DbContext context)
    {
        var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        return scope;
    }

    private List<IEntityType> TenantOwnedEntityTypes()
    {
        using var scope = App.Services.CreateScope();
        return [.. App.Services.GetServices<ModuleDatabase>()
            .SelectMany(database => ((DbContext)scope.ServiceProvider.GetRequiredService(database.ContextType)).Model.GetEntityTypes())
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))];
    }

    private (Type ContextType, IEntityType EntityType) Locate(Type clrType)
    {
        using var scope = App.Services.CreateScope();
        return App.Services.GetServices<ModuleDatabase>()
            .Select(database => (database.ContextType,
                EntityType: ((DbContext)scope.ServiceProvider.GetRequiredService(database.ContextType)).Model.FindEntityType(clrType)))
            .Where(found => found.EntityType is not null)
            .Select(found => (found.ContextType, found.EntityType!))
            .Single();
    }

    private static string TableName(IEntityType entityType) =>
        $"\"{entityType.GetSchema()}\".\"{entityType.GetTableName()}\"";

    private async Task<long> CountAsTenantAsync(Guid tenantId, string table, Guid id)
    {
        await using var connection = await OpenAsTenantAsync(tenantId);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task<int> ExecuteAsTenantAsync(Guid tenantId, string sql, Guid id, string? row = null)
    {
        await using var connection = await OpenAsTenantAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        if (row is not null)
        {
            command.Parameters.AddWithValue("row", row);
        }

        return await command.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>The row as <c>sp_platform</c> (BYPASSRLS) sees it, with a new ID so only RLS can refuse the copy.</summary>
    private async Task<string> RowAsJsonWithNewIdAsync(string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString(DatabaseRoles.Platform));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            $"SELECT jsonb_set(to_jsonb(t), '{{id}}', to_jsonb(@newId)) FROM {table} AS t WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("newId", Guid.CreateVersion7().ToString());
        return (string)(await command.ExecuteScalarAsync(Ct))!;
    }

    /// <summary><c>sp_app</c> with <c>app.tenant_id</c> set, as the connection interceptor does — but outside EF.</summary>
    private async Task<NpgsqlConnection> OpenAsTenantAsync(Guid tenantId)
    {
        var connection = new NpgsqlConnection(Fixture.ConnectionString(DatabaseRoles.App));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT set_config('{TenantConnectionInterceptor.Setting}', @tenant, false)", connection);
        command.Parameters.AddWithValue("tenant", tenantId.ToString());
        await command.ExecuteNonQueryAsync(Ct);
        return connection;
    }
}
