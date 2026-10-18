using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

public static class SeedingServiceCollectionExtensions
{
    /// <summary>Registers a module's seeder; it is resolved in its own scope when it runs.</summary>
    public static IServiceCollection AddSeeder<TSeeder>(this IServiceCollection services)
        where TSeeder : class, ISeeder
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ISeeder, TSeeder>();
        return services;
    }

    /// <summary>The seed runner, for the module that owns the bookkeeping (<see cref="ISeedRunLog"/>).</summary>
    public static IServiceCollection AddSeedRunner<TRunLog>(
        this IServiceCollection services, Func<IServiceProvider, string> environmentName)
        where TRunLog : class, ISeedRunLog
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environmentName);

        services.TryAddSingleton<OwnerContextFactory>();
        services.TryAddSingleton<DatabaseReset>();
        services.TryAddSingleton(provider => new SeedEnvironment(environmentName(provider)));
        services.TryAddSingleton<SeedRunner>();
        services.TryAddScoped<ISeedRunLog, TRunLog>();
        return services;
    }
}
