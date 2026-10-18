using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Audit;

namespace SportsPlatform.IntegrationTests.Audit;

/// <summary>AR-19: audit entries can be appended and read, never changed or removed, by any role.</summary>
public sealed class AuditEntriesAppendOnlyTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _table = "audit.audit_entries";

    public static TheoryData<string, string> RuntimeRoleChanges => new()
    {
        { DatabaseRoles.App, $"UPDATE {_table} SET reason = 'changed'" },
        { DatabaseRoles.App, $"DELETE FROM {_table}" },
        { DatabaseRoles.App, $"TRUNCATE {_table}" },
        { DatabaseRoles.Platform, $"UPDATE {_table} SET reason = 'changed'" },
        { DatabaseRoles.Platform, $"DELETE FROM {_table}" },
        { DatabaseRoles.Platform, $"TRUNCATE {_table}" },
    };

    public static TheoryData<string> OwnerChanges => new()
    {
        $"UPDATE {_table} SET reason = 'changed'",
        $"DELETE FROM {_table}",
        $"TRUNCATE {_table}",
    };

    [Theory]
    [MemberData(nameof(RuntimeRoleChanges))]
    public async Task Runtime_roles_cannot_change_audit_entries(string role, string sql)
    {
        await AppendAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(role, sql));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().Reason.ShouldBe("Kawempe fixture consumer fixed");
    }

    [Theory]
    [MemberData(nameof(OwnerChanges))]
    public async Task The_trigger_stops_the_owner_changing_audit_entries(string sql)
    {
        // FORCE ROW LEVEL SECURITY binds the owner too: it sees the entry only under the entry's tenant, and an UPDATE
        // or DELETE that matches no row would never reach the row trigger.
        var tenantId = Guid.CreateVersion7();
        await App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record() with { TenantId = tenantId }, Ct);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            DatabaseRoles.Migrator, $"SELECT set_config('app.tenant_id', '{tenantId}', false); {sql}"));

        exception.SqlState.ShouldBe(TableAccessMigrationExtensions.AppendOnlyViolationSqlState);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().Reason.ShouldBe("Kawempe fixture consumer fixed");
    }

    // The app appends as sp_platform (PlatformAuditDbContext).
    private Task AppendAsync() => App.Services.GetRequiredService<IAuditLog>().AppendAsync(AuditTestData.Record(), Ct);
}
