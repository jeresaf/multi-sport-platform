namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>The three named connection strings, one per database role (docs/architecture/multi-tenancy.md).</summary>
public static class ConnectionStringNames
{
    /// <summary><c>sp_app</c>: the API and module code; no DDL, subject to RLS.</summary>
    public const string App = "App";

    /// <summary><c>sp_migrator</c>: owns the schemas; migrations and reference seeds only.</summary>
    public const string Migrator = "Migrator";

    /// <summary><c>sp_platform</c>: BYPASSRLS; explicit platform services only.</summary>
    public const string Platform = "Platform";
}
