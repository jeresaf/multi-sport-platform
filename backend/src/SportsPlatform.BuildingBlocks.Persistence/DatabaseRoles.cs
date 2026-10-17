namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>The database roles created by <c>infrastructure/local/postgres/init/01-roles.sh</c> (and in each hosted environment).</summary>
public static class DatabaseRoles
{
    /// <summary>Owns schemas and tables; runs migrations and reference seeds.</summary>
    public const string Migrator = "sp_migrator";

    /// <summary>The API's role: data access only, subject to RLS.</summary>
    public const string App = "sp_app";

    /// <summary>BYPASSRLS, used only by explicit platform services.</summary>
    public const string Platform = "sp_platform";

    /// <summary>The roles the application runs as; the grant helpers narrow what they may do.</summary>
    public const string RuntimeRoles = App + ", " + Platform;
}
