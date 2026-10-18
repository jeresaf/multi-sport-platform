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
            .UseModuleDefaults<TContext>(RequiredConnectionString(configuration, ConnectionStringNames.App), schema)
            // Order matters: rows added by domain event handlers are still checked for their tenant and get their
            // audit columns.
            .AddInterceptors(
                provider.GetRequiredService<MessagingInterceptor>(),
                provider.GetRequiredService<TenantOwnershipInterceptor>(),
                provider.GetRequiredService<AuditColumnsInterceptor>(),
                provider.GetRequiredService<TenantConnectionInterceptor>()));

        services.AddSingleton(new ModuleDatabase(
            schema,
            typeof(TContext),
            connectionString => (TContext)Activator.CreateInstance(
                typeof(TContext), ModuleDbContextOptions.ForMigrations<TContext>(connectionString, schema), new TenantContext())!));

        return services;
    }

    internal static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
