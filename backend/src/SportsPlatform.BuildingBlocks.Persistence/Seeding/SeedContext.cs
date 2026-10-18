using Microsoft.EntityFrameworkCore;

namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>What a seeder runs with: its own service scope (as <c>sp_app</c>) and, for REFERENCE and CONFIGURATION, owner contexts.</summary>
public sealed class SeedContext : IAsyncDisposable
{
    private readonly OwnerContextFactory _ownerContexts;
    private readonly List<DbContext> _created = [];

    internal SeedContext(SeedCategory category, IServiceProvider services, OwnerContextFactory ownerContexts)
    {
        Category = category;
        Services = services;
        _ownerContexts = ownerContexts;
    }

    public SeedCategory Category { get; }

    /// <summary>A scope for this seeder only: application commands, module DbContexts on the <c>App</c> connection.</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// A module DbContext on the <c>Migrator</c> connection, for the read-only global tables. Only REFERENCE and
    /// CONFIGURATION seeders get one; the context is disposed with this seed context.
    /// </summary>
    public TContext CreateOwnerContext<TContext>()
        where TContext : ModuleDbContext
    {
        if (!Category.RunsAsOwner())
        {
            throw new InvalidOperationException(
                $"A {Category.Code()} seeder runs as sp_app through application commands, never as the owner.");
        }

        var context = _ownerContexts.Create<TContext>();
        _created.Add(context);
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _created)
        {
            await context.DisposeAsync();
        }

        _created.Clear();
    }
}
