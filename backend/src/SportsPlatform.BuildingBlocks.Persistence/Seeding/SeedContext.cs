using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// What a seeder runs with: its own service scope (as <c>sp_app</c>); for REFERENCE and CONFIGURATION, owner contexts;
/// for DEMO and TEST, application commands run as a tenant and audited (<see cref="RunCommandAsync"/>).
/// </summary>
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

    /// <summary>
    /// Runs one application command as it would run in a request, in a new scope of its own (DEMO and TEST only): the
    /// scope runs under <paramref name="tenantId"/> (set once, before the command opens a connection, S02 D1; null for
    /// none) as the system actor, and its audit begins with <paramref name="action"/> and source <c>SYSTEM</c>. The
    /// command's first save commits the audit entry with the change. A seeder calls a command only for a change: one
    /// that saves nothing, or fails, leaves its audit unwritten and throws, which stops the run.
    /// </summary>
    public async Task<T> RunCommandAsync<T>(
        ActionDefinition action,
        Guid? tenantId,
        Func<IServiceProvider, CancellationToken, Task<T>> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(command);
        if (Category.RunsAsOwner())
        {
            throw new InvalidOperationException(
                $"A {Category.Code()} seeder writes global tables as the owner; it runs no application commands.");
        }

        await using var scope = CreateTenantScope(tenantId);
        var audit = scope.ServiceProvider.GetRequiredService<AuditScope>();
        audit.Begin(action, AuditSource.System);

        var result = await command(scope.ServiceProvider, cancellationToken);
        if (audit.IsPending)
        {
            throw new InvalidOperationException(
                $"The seeded command '{action.Code}' saved nothing, so its audit entry was not written; a seeder runs a " +
                "command only for a change.");
        }

        return result;
    }

    /// <summary>
    /// Reads what a seeder needs to decide which commands to run, in a new scope under <paramref name="tenantId"/>
    /// (tenant-owned tables are visible only under their tenant). Nothing is audited: run changes through
    /// <see cref="RunCommandAsync"/>.
    /// </summary>
    public async Task<T> QueryAsTenantAsync<T>(
        Guid tenantId, Func<IServiceProvider, CancellationToken, Task<T>> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Category.RunsAsOwner())
        {
            throw new InvalidOperationException(
                $"A {Category.Code()} seeder writes global tables as the owner; it reads no tenant's data.");
        }

        await using var scope = CreateTenantScope(tenantId);
        return await query(scope.ServiceProvider, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _created)
        {
            await context.DisposeAsync();
        }

        _created.Clear();
    }

    private AsyncServiceScope CreateTenantScope(Guid? tenantId)
    {
        var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return scope;
    }
}
