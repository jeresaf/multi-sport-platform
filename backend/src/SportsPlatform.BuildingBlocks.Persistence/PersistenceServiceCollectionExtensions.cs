using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module's DbContext on the <c>App</c> connection and records it for the migration runner.
    /// Migrations run in registration order, which follows the Host's module list.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : ModuleDbContext
    {
        AddContext<TContext>(services, configuration, schema, ConnectionStringNames.App);

        services.AddSingleton(new ModuleDatabase(
            schema,
            typeof(TContext),
            connectionString => (TContext)Activator.CreateInstance(
                typeof(TContext), ModuleDbContextOptions.ForMigrations<TContext>(connectionString, schema), new TenantContext())!));

        return services;
    }

    /// <summary>
    /// Registers a platform service's DbContext on the <c>Platform</c> connection (<c>sp_platform</c>, BYPASSRLS): the
    /// named exceptions that read or write across tenants (docs/architecture/multi-tenancy.md). It maps tables its
    /// module's migrated context owns and is never migrated itself, so it has no <see cref="ModuleDatabase"/>.
    /// </summary>
    public static IServiceCollection AddPlatformServiceDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : ModuleDbContext
    {
        AddContext<TContext>(services, configuration, schema, ConnectionStringNames.Platform);
        return services;
    }

    private static void AddContext<TContext>(
        IServiceCollection services, IConfiguration configuration, string schema, string connectionName)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddScoped<MessagingInterceptor>();
        services.TryAddScoped<AuditColumnsInterceptor>();
        services.TryAddScoped<TenantOwnershipInterceptor>();
        services.TryAddScoped<TenantConnectionInterceptor>();
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<CorrelationContext>();
        services.TryAddScoped<AuditScope>();
        services.TryAddSingleton<MigrationRunner>();

        // The connection string is read when a context is created, so hosts that never touch the database
        // (and tests that don't need one) start without it.
        services.AddDbContext<TContext>((provider, options) => options
            .UseModuleDefaults<TContext>(RequiredConnectionString(configuration, connectionName), schema)
            // Order matters: rows added by domain event handlers are still checked for their tenant and get their
            // audit columns.
            .AddInterceptors(
                provider.GetRequiredService<MessagingInterceptor>(),
                provider.GetRequiredService<TenantOwnershipInterceptor>(),
                provider.GetRequiredService<AuditColumnsInterceptor>(),
                provider.GetRequiredService<TenantConnectionInterceptor>()));
    }

    internal static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
