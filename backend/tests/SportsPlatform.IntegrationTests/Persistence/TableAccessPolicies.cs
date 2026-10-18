using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary>What the runtime roles (<c>sp_app</c>, <c>sp_platform</c>) may do with a table.</summary>
public enum TableAccess
{
    /// <summary>The default grants from <c>01-roles.sh</c>: SELECT, INSERT, UPDATE, DELETE.</summary>
    ReadWrite,

    /// <summary>Reference and global tables: SELECT only (<c>ReadOnlyForRuntimeRoles</c>).</summary>
    ReadOnly,

    /// <summary>Audit and timeline tables: SELECT and INSERT, plus the triggers (<c>AppendOnly</c>).</summary>
    AppendOnly,

    /// <summary>Each schema's migrations history: no access (revoked by <c>MigrationRunner</c>).</summary>
    NoAccess,
}

/// <summary>
/// The declared access policy of every table. <see cref="TableGrantsTests"/> compares it with the database, so a new
/// table fails until it is added here, and a table whose grants drift from its policy fails too.
/// </summary>
public static class TableAccessPolicies
{
    /// <summary>Every table except the migrations history tables (those are always <see cref="TableAccess.NoAccess"/>).</summary>
    public static IReadOnlyDictionary<string, TableAccess> Tables { get; } = new Dictionary<string, TableAccess>
    {
        // Every module schema gets an outbox and an inbox (ModuleDbContext); the processor updates and S01-T5b re-drives.
        // Audit entries (S01-T6, AR-19).
        ["audit.audit_entries"] = TableAccess.AppendOnly,

        // Seed bookkeeping (S01-T7): written by the seed runner as the owner, never changed.
        ["platform.seed_runs"] = TableAccess.AppendOnly,

        ["platform.outbox_messages"] = TableAccess.ReadWrite,
        ["platform.inbox_messages"] = TableAccess.ReadWrite,
        ["conventions_test.outbox_messages"] = TableAccess.ReadWrite,
        ["conventions_test.inbox_messages"] = TableAccess.ReadWrite,
        ["messaging_test.outbox_messages"] = TableAccess.ReadWrite,
        ["messaging_test.inbox_messages"] = TableAccess.ReadWrite,
        ["messaging_test.probes"] = TableAccess.ReadWrite,
        ["messaging_test.received_probes"] = TableAccess.ReadWrite,
        ["conventions_test.convention_probes"] = TableAccess.ReadWrite,
        ["conventions_test.reference_probes"] = TableAccess.ReadOnly,
        ["conventions_test.append_only_probes"] = TableAccess.AppendOnly,
    };

    /// <summary>
    /// Deliberate departures from a table's policy for one role, e.g. a platform service that must write a global
    /// table. Key: (table, role); value: the role's privileges on that table and why.
    /// </summary>
    public static IReadOnlyDictionary<(string Table, string Role), (string[] Privileges, string Reason)> Exceptions { get; } =
        new Dictionary<(string Table, string Role), (string[] Privileges, string Reason)>();

    public static string[] Privileges(TableAccess access) => access switch
    {
        TableAccess.ReadWrite => ["SELECT", "INSERT", "UPDATE", "DELETE"],
        TableAccess.ReadOnly => ["SELECT"],
        TableAccess.AppendOnly => ["SELECT", "INSERT"],
        TableAccess.NoAccess => [],
        _ => throw new ArgumentOutOfRangeException(nameof(access), access, null),
    };

    public static string[] RuntimeRoles { get; } = [DatabaseRoles.App, DatabaseRoles.Platform];
}
