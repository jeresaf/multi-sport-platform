using Microsoft.EntityFrameworkCore;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>Provider settings shared by the runtime, the migration runner and design-time factories.</summary>
public static class ModuleDbContextOptions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static DbContextOptionsBuilder UseModuleDefaults<TContext>(
        this DbContextOptionsBuilder builder, string connectionString, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        return builder
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(MigrationsHistoryTable, schema)
                .MigrationsAssembly(typeof(TContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();
    }

    /// <summary>Options for a context that only migrates (no interceptors).</summary>
    public static DbContextOptions<TContext> ForMigrations<TContext>(string connectionString, string schema)
        where TContext : ModuleDbContext =>
        (DbContextOptions<TContext>)new DbContextOptionsBuilder<TContext>()
            .UseModuleDefaults<TContext>(connectionString, schema)
            .Options;
}
