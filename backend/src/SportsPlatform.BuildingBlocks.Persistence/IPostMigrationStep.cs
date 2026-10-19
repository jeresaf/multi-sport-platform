using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Data work that must follow a migration but needs configuration a migration can't read (e.g. the platform's base
/// domain). <see cref="MigrationRunner"/> runs every step after all migrations, in registration order, each time it
/// runs, so a step must be idempotent. Steps write as the owner through <see cref="OwnerContextFactory"/>, so like
/// owner seeds they can't write tenant-owned rows.
/// </summary>
public interface IPostMigrationStep
{
    /// <summary>A short name for the log, e.g. <c>tenancy.platform-subdomains</c>.</summary>
    string Name { get; }

    /// <summary>Applies the step; returns how many rows it changed (0 on a repeat run).</summary>
    Task<int> RunAsync(CancellationToken cancellationToken);
}

public static class PostMigrationStepServiceCollectionExtensions
{
    public static IServiceCollection AddPostMigrationStep<TStep>(this IServiceCollection services)
        where TStep : class, IPostMigrationStep
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<OwnerContextFactory>();
        services.AddSingleton<IPostMigrationStep, TStep>();
        return services;
    }
}
